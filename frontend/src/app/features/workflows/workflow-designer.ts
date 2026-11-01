import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnInit,
  Output,
  ViewChild,
  computed,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type NodeType = 'START' | 'DEPARTMENT' | 'APPROVAL' | 'DECISION' | 'CONFIRMATION' | 'END';

export interface DesignerNode {
  id: string;
  type: NodeType;
  name: string;
  x: number;
  y: number;
  departmentId?: string;
  departmentName?: string;
  roleId?: string;
  roleName?: string;
  description?: string;
  slaHours?: number;
  approverType?: 'ROLE' | 'DEPARTMENT_LEAD' | 'SPECIFIC_USER';
  milestone?: 'RECEIVE' | 'RESULT' | 'RESOLUTION';
  trigger?: string;
  completionState?: string;
  conditions?: {
    id: string;
    field: string;
    operator: string;
    value: string;
    label: string;
    targetNodeId: string;
  }[];
}

export interface DesignerEdge {
  id: string;
  source: string;
  target: string;
  label?: string;
  guard?: Record<string, unknown>;
}

export interface ValidationIssueItem {
  id: string;
  type: 'error' | 'warning';
  title: string;
  cause: string;
  suggestion: string;
  nodeId?: string;
  nodeName?: string;
}

export interface LookupItem {
  id: string;
  name: string;
  code?: string;
}

interface HistorySnapshot {
  nodes: DesignerNode[];
  edges: DesignerEdge[];
}

@Component({
  selector: 'bf-workflow-designer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './workflow-designer.html',
  styleUrls: ['./workflow-designer.scss'],
})
export class WorkflowDesigner implements OnInit {
  @ViewChild('canvasContainer') canvasContainer!: ElementRef<HTMLDivElement>;

  @Input() workflowId = '';
  @Input() workflowName = '';
  @Input() businessType: 'TASK' | 'REQUEST' = 'REQUEST';
  @Input() versionNo = 1;
  @Input() versionStatus = 'DRAFT';
  @Input() readonly = false;
  @Input() departments: LookupItem[] = [];
  @Input() roles: LookupItem[] = [];

  @Input() set initialDefinition(def: Record<string, unknown> | null) {
    if (def && typeof def === 'object') {
      this.loadFromDefinition(def);
    }
  }

  @Output() definitionChange = new EventEmitter<string>();
  @Output() saveDraft = new EventEmitter<void>();
  @Output() validateWorkflow = new EventEmitter<void>();
  @Output() publishWorkflow = new EventEmitter<void>();

  // Reactive state
  readonly nodes = signal<DesignerNode[]>([]);
  readonly edges = signal<DesignerEdge[]>([]);
  readonly selectedNode = signal<DesignerNode | null>(null);
  readonly selectedEdge = signal<DesignerEdge | null>(null);

  readonly zoom = signal(1.0);
  readonly zoomPercentage = computed(() => Math.round(this.zoom() * 100));
  readonly panX = signal(60);
  readonly panY = signal(60);

  readonly isPanning = signal(false);
  private panStart = { x: 0, y: 0 };
  private panStartOffset = { x: 0, y: 0 };

  // Dragging existing node
  readonly draggingNodeId = signal<string | null>(null);
  private dragNodeOffset = { x: 0, y: 0 };

  // Connecting line in progress
  readonly connectingFrom = signal<{ nodeId: string } | null>(null);
  readonly previewMousePos = signal<{ x: number; y: number } | null>(null);

  // History stack for undo/redo
  private undoStack: HistorySnapshot[] = [];
  private redoStack: HistorySnapshot[] = [];

  // Validation Drawer
  readonly showValidation = signal(false);
  readonly validationIssues = signal<ValidationIssueItem[]>([]);

  ngOnInit() {
    if (this.nodes().length === 0) {
      this.initDefaultGraph();
    }
  }

  private initDefaultGraph() {
    const startNode: DesignerNode = {
      id: 'node-start',
      type: 'START',
      name: this.businessType === 'REQUEST' ? 'Nộp yêu cầu' : 'Tạo nhiệm vụ',
      x: 80,
      y: 180,
      trigger: this.businessType === 'REQUEST' ? 'REQUEST_SUBMITTED' : 'TASK_CREATED',
    };

    const initialDeptNode: DesignerNode = {
      id: 'node-step-1',
      type: 'DEPARTMENT',
      name: 'Tiếp nhận & Xử lý ban đầu',
      x: 320,
      y: 160,
      departmentId: this.departments[0]?.id || '',
      departmentName: this.departments[0]?.name || 'Phòng ban chuyên trách',
      slaHours: 24,
      description: 'Tiếp nhận hồ sơ và kiểm tra thông tin ban đầu',
    };

    const endNode: DesignerNode = {
      id: 'node-end',
      type: 'END',
      name: 'Hoàn tất quy trình',
      x: 620,
      y: 180,
      completionState: 'COMPLETED',
    };

    const initialEdges: DesignerEdge[] = [
      { id: 'e-1', source: 'node-start', target: 'node-step-1', label: '' },
      { id: 'e-2', source: 'node-step-1', target: 'node-end', label: '' },
    ];

    this.nodes.set([startNode, initialDeptNode, endNode]);
    this.edges.set(initialEdges);
    this.recordHistory();
    this.emitChange();
  }

  private loadFromDefinition(def: Record<string, unknown>) {
    try {
      const rawNodes = Array.isArray(def['nodes']) ? (def['nodes'] as DesignerNode[]) : [];
      const rawEdges = Array.isArray(def['edges']) ? (def['edges'] as DesignerEdge[]) : [];

      if (rawNodes.length > 0) {
        this.nodes.set(rawNodes);
        this.edges.set(rawEdges);
        this.undoStack = [];
        this.redoStack = [];
        this.recordHistory();
        return;
      }
    } catch {
      // fallback to initial
    }
    this.initDefaultGraph();
  }

  getDefinitionJson(): string {
    return JSON.stringify({
      nodes: this.nodes(),
      edges: this.edges(),
      updatedAt: new Date().toISOString(),
    });
  }

  private emitChange() {
    this.definitionChange.emit(this.getDefinitionJson());
  }

  private recordHistory() {
    this.undoStack.push({
      nodes: JSON.parse(JSON.stringify(this.nodes())),
      edges: JSON.parse(JSON.stringify(this.edges())),
    });
    if (this.undoStack.length > 30) this.undoStack.shift();
    this.redoStack = [];
  }

  undo() {
    if (this.readonly || this.undoStack.length <= 1) return;
    const current = this.undoStack.pop()!;
    this.redoStack.push(current);
    const prev = this.undoStack[this.undoStack.length - 1];
    this.nodes.set(JSON.parse(JSON.stringify(prev.nodes)));
    this.edges.set(JSON.parse(JSON.stringify(prev.edges)));
    this.selectedNode.set(null);
    this.selectedEdge.set(null);
    this.emitChange();
  }

  redo() {
    if (this.readonly || this.redoStack.length === 0) return;
    const next = this.redoStack.pop()!;
    this.undoStack.push(next);
    this.nodes.set(JSON.parse(JSON.stringify(next.nodes)));
    this.edges.set(JSON.parse(JSON.stringify(next.edges)));
    this.selectedNode.set(null);
    this.selectedEdge.set(null);
    this.emitChange();
  }

  // --- PALETTE DRAG & ADD ---
  onPaletteDragStart(event: DragEvent, type: NodeType) {
    if (this.readonly) return;
    event.dataTransfer?.setData('application/bizflow-node-type', type);
  }

  onCanvasDragOver(event: DragEvent) {
    if (this.readonly) return;
    event.preventDefault();
  }

  onCanvasDrop(event: DragEvent) {
    if (this.readonly) return;
    event.preventDefault();
    const type = event.dataTransfer?.getData('application/bizflow-node-type') as NodeType;
    if (!type) return;

    const rect = this.canvasContainer.nativeElement.getBoundingClientRect();
    const clientX = event.clientX - rect.left;
    const clientY = event.clientY - rect.top;

    const canvasX = Math.round((clientX - this.panX()) / this.zoom());
    const canvasY = Math.round((clientY - this.panY()) / this.zoom());

    this.addNode(type, canvasX - 100, canvasY - 35);
  }

  addNode(type: NodeType, x?: number, y?: number) {
    if (this.readonly) return;

    const defaultNames: Record<NodeType, string> = {
      START: 'Sự kiện bắt đầu',
      DEPARTMENT: 'Xử lý phòng ban',
      APPROVAL: 'Phê duyệt cấp quản lý',
      DECISION: 'Phân loại điều kiện',
      CONFIRMATION: 'Xác nhận kết quả',
      END: 'Kết thúc quy trình',
    };

    const posX = x ?? Math.round((300 - this.panX()) / this.zoom());
    const posY = y ?? Math.round((200 - this.panY()) / this.zoom());

    const newNode: DesignerNode = {
      id: `node-${Date.now().toString(36)}`,
      type,
      name: defaultNames[type],
      x: posX,
      y: posY,
    };

    if (type === 'DEPARTMENT') {
      newNode.departmentId = this.departments[0]?.id || '';
      newNode.departmentName = this.departments[0]?.name || 'Chọn phòng ban';
      newNode.slaHours = 24;
    } else if (type === 'APPROVAL') {
      newNode.approverType = 'ROLE';
      newNode.roleId = this.roles[0]?.id || '';
      newNode.roleName = this.roles[0]?.name || 'Người duyệt';
    } else if (type === 'CONFIRMATION') {
      newNode.milestone = 'RESULT';
    } else if (type === 'DECISION') {
      newNode.conditions = [
        { id: 'c1', field: 'priority', operator: '=', value: 'HIGH', label: 'Ưu tiên cao', targetNodeId: '' },
        { id: 'c2', field: 'default', operator: 'default', value: '', label: 'Mặc định', targetNodeId: '' },
      ];
    }

    this.nodes.update((list) => [...list, newNode]);
    this.selectNode(newNode);
    this.recordHistory();
    this.emitChange();
  }

  // --- SELECTION ---
  selectNode(node: DesignerNode | null, event?: MouseEvent) {
    event?.stopPropagation();
    this.selectedNode.set(node);
    this.selectedEdge.set(null);
  }

  selectEdge(edge: DesignerEdge | null, event?: MouseEvent) {
    event?.stopPropagation();
    this.selectedEdge.set(edge);
    this.selectedNode.set(null);
  }

  onCanvasClick() {
    this.selectedNode.set(null);
    this.selectedEdge.set(null);
    this.cancelConnecting();
  }

  // --- NODE DRAGGING ON CANVAS ---
  startNodeDrag(node: DesignerNode, event: MouseEvent) {
    if (this.readonly || event.button !== 0) return;
    event.stopPropagation();
    this.selectNode(node);
    this.draggingNodeId.set(node.id);

    this.dragNodeOffset = {
      x: event.clientX - node.x * this.zoom(),
      y: event.clientY - node.y * this.zoom(),
    };
  }

  // --- PANNING CANVAS ---
  startPan(event: MouseEvent) {
    // Only pan if left click on canvas or middle click
    if (event.button === 0 || event.button === 1) {
      this.isPanning.set(true);
      this.panStart = { x: event.clientX, y: event.clientY };
      this.panStartOffset = { x: this.panX(), y: this.panY() };
    }
  }

  @HostListener('window:mousemove', ['$event'])
  onWindowMouseMove(event: MouseEvent) {
    if (this.isPanning()) {
      const dx = event.clientX - this.panStart.x;
      const dy = event.clientY - this.panStart.y;
      this.panX.set(this.panStartOffset.x + dx);
      this.panY.set(this.panStartOffset.y + dy);
      return;
    }

    const draggingId = this.draggingNodeId();
    if (draggingId) {
      const newX = Math.round((event.clientX - this.dragNodeOffset.x) / this.zoom());
      const newY = Math.round((event.clientY - this.dragNodeOffset.y) / this.zoom());

      this.nodes.update((list) =>
        list.map((n) => (n.id === draggingId ? { ...n, x: Math.max(10, newX), y: Math.max(10, newY) } : n))
      );

      // sync selectedNode coordinates
      const currentSelected = this.selectedNode();
      if (currentSelected && currentSelected.id === draggingId) {
        this.selectedNode.set({ ...currentSelected, x: newX, y: newY });
      }
      return;
    }

    if (this.connectingFrom()) {
      const rect = this.canvasContainer?.nativeElement.getBoundingClientRect();
      if (rect) {
        const clientX = event.clientX - rect.left;
        const clientY = event.clientY - rect.top;
        this.previewMousePos.set({
          x: (clientX - this.panX()) / this.zoom(),
          y: (clientY - this.panY()) / this.zoom(),
        });
      }
    }
  }

  @HostListener('window:mouseup')
  onWindowMouseUp() {
    if (this.isPanning()) {
      this.isPanning.set(false);
    }
    if (this.draggingNodeId()) {
      this.draggingNodeId.set(null);
      this.recordHistory();
      this.emitChange();
    }
  }

  // --- CONNECTING PORTS ---
  startConnecting(node: DesignerNode, event: MouseEvent) {
    if (this.readonly) return;
    event.stopPropagation();
    if (node.type === 'END') return; // END has no outgoing port

    this.connectingFrom.set({ nodeId: node.id });
    const rect = this.canvasContainer.nativeElement.getBoundingClientRect();
    this.previewMousePos.set({
      x: (event.clientX - rect.left - this.panX()) / this.zoom(),
      y: (event.clientY - rect.top - this.panY()) / this.zoom(),
    });
  }

  completeConnection(targetNode: DesignerNode, event: MouseEvent) {
    if (this.readonly) return;
    event.stopPropagation();
    const source = this.connectingFrom();
    if (!source) return;

    if (source.nodeId === targetNode.id) {
      this.cancelConnecting();
      return;
    }

    if (targetNode.type === 'START') {
      this.cancelConnecting();
      return;
    }

    // Check duplicate
    const exists = this.edges().some((e) => e.source === source.nodeId && e.target === targetNode.id);
    if (!exists) {
      const newEdge: DesignerEdge = {
        id: `e-${Date.now().toString(36)}`,
        source: source.nodeId,
        target: targetNode.id,
        label: '',
      };
      this.edges.update((list) => [...list, newEdge]);
      this.recordHistory();
      this.emitChange();
    }

    this.cancelConnecting();
  }

  cancelConnecting() {
    this.connectingFrom.set(null);
    this.previewMousePos.set(null);
  }

  // --- DELETION ---
  deleteSelectedNode() {
    if (this.readonly) return;
    const node = this.selectedNode();
    if (!node) return;

    const connectedEdges = this.edges().filter((e) => e.source === node.id || e.target === node.id);
    if (connectedEdges.length > 0) {
      if (!confirm(`Xóa bước "${node.name}" sẽ đồng thời xóa ${connectedEdges.length} liên kết kết nối tới bước này. Bạn có chắc chắn?`)) {
        return;
      }
    }

    this.nodes.update((list) => list.filter((n) => n.id !== node.id));
    this.edges.update((list) => list.filter((e) => e.source !== node.id && e.target !== node.id));
    this.selectedNode.set(null);
    this.recordHistory();
    this.emitChange();
  }

  deleteSelectedEdge() {
    if (this.readonly) return;
    const edge = this.selectedEdge();
    if (!edge) return;

    this.edges.update((list) => list.filter((e) => e.id !== edge.id));
    this.selectedEdge.set(null);
    this.recordHistory();
    this.emitChange();
  }

  // --- AUTO LAYOUT (TỰ ĐỘNG SẮP XẾP) ---
  autoLayout() {
    if (this.readonly) return;
    const allNodes = [...this.nodes()];
    const allEdges = [...this.edges()];
    if (allNodes.length === 0) return;

    const startNode = allNodes.find((n) => n.type === 'START') || allNodes[0];

    // Compute topological layers using BFS
    const layers = new Map<string, number>();
    layers.set(startNode.id, 0);

    const queue: string[] = [startNode.id];
    while (queue.length > 0) {
      const curr = queue.shift()!;
      const currLayer = layers.get(curr)!;
      const outgoing = allEdges.filter((e) => e.source === curr).map((e) => e.target);
      for (const next of outgoing) {
        if (!layers.has(next) || layers.get(next)! < currLayer + 1) {
          layers.set(next, currLayer + 1);
          queue.push(next);
        }
      }
    }

    // Assign any unvisited nodes to max layer
    let maxLayer = 0;
    layers.forEach((l) => (maxLayer = Math.max(maxLayer, l)));
    allNodes.forEach((n) => {
      if (!layers.has(n.id)) {
        layers.set(n.id, ++maxLayer);
      }
    });

    // Group nodes by layer
    const layerBuckets = new Map<number, DesignerNode[]>();
    allNodes.forEach((n) => {
      const l = layers.get(n.id) || 0;
      if (!layerBuckets.has(l)) layerBuckets.set(l, []);
      layerBuckets.get(l)!.push(n);
    });

    const newNodes = allNodes.map((n) => {
      const l = layers.get(n.id) || 0;
      const bucket = layerBuckets.get(l) || [n];
      const indexInBucket = bucket.findIndex((item) => item.id === n.id);

      const x = 80 + l * 290;
      const y = 140 + indexInBucket * 160;

      return { ...n, x, y };
    });

    this.nodes.set(newNodes);
    this.fitView();
    this.recordHistory();
    this.emitChange();
  }

  // --- ZOOM & FIT CONTROLS ---
  zoomIn() {
    this.zoom.update((z) => Math.min(2.0, Math.round((z + 0.1) * 10) / 10));
  }

  zoomOut() {
    this.zoom.update((z) => Math.max(0.5, Math.round((z - 0.1) * 10) / 10));
  }

  resetZoom() {
    this.zoom.set(1.0);
    this.panX.set(60);
    this.panY.set(60);
  }

  fitView() {
    const list = this.nodes();
    if (list.length === 0) return;

    let minX = Infinity,
      minY = Infinity,
      maxX = -Infinity,
      maxY = -Infinity;
    list.forEach((n) => {
      minX = Math.min(minX, n.x);
      minY = Math.min(minY, n.y);
      maxX = Math.max(maxX, n.x + 230);
      maxY = Math.max(maxY, n.y + 120);
    });

    const rect = this.canvasContainer?.nativeElement.getBoundingClientRect();
    if (!rect) return;

    const width = maxX - minX + 160;
    const height = maxY - minY + 160;

    const scaleX = rect.width / width;
    const scaleY = rect.height / height;
    const newZoom = Math.min(1.2, Math.max(0.6, Math.min(scaleX, scaleY)));

    this.zoom.set(newZoom);
    this.panX.set(Math.round((rect.width - (maxX + minX) * newZoom) / 2));
    this.panY.set(Math.round((rect.height - (maxY + minY) * newZoom) / 2));
  }

  // --- CURVE SVG PATH COMPUTATION ---
  getNodePortPos(nodeId: string, portType: 'in' | 'out'): { x: number; y: number } {
    const node = this.nodes().find((n) => n.id === nodeId);
    if (!node) return { x: 0, y: 0 };

    const nodeWidth = node.type === 'START' || node.type === 'END' ? 170 : 230;
    const nodeHeight = 85;

    if (portType === 'in') {
      return { x: node.x, y: node.y + nodeHeight / 2 };
    } else {
      return { x: node.x + nodeWidth, y: node.y + nodeHeight / 2 };
    }
  }

  getEdgePath(edge: DesignerEdge): string {
    const start = this.getNodePortPos(edge.source, 'out');
    const end = this.getNodePortPos(edge.target, 'in');

    const dx = Math.max(40, Math.abs(end.x - start.x) * 0.5);
    return `M ${start.x} ${start.y} C ${start.x + dx} ${start.y}, ${end.x - dx} ${end.y}, ${end.x} ${end.y}`;
  }

  getEdgeMidpoint(edge: DesignerEdge): { x: number; y: number } {
    const start = this.getNodePortPos(edge.source, 'out');
    const end = this.getNodePortPos(edge.target, 'in');
    return {
      x: (start.x + end.x) / 2,
      y: (start.y + end.y) / 2,
    };
  }

  getPreviewEdgePath(): string {
    const source = this.connectingFrom();
    const mouse = this.previewMousePos();
    if (!source || !mouse) return '';

    const start = this.getNodePortPos(source.nodeId, 'out');
    const dx = Math.max(40, Math.abs(mouse.x - start.x) * 0.5);
    return `M ${start.x} ${start.y} C ${start.x + dx} ${start.y}, ${mouse.x - dx} ${mouse.y}, ${mouse.x} ${mouse.y}`;
  }

  // --- INSPECTOR PROPERTY UPDATES ---
  onDepartmentSelect(deptId: string) {
    const node = this.selectedNode();
    if (!node) return;
    const found = this.departments.find((d) => d.id === deptId);
    this.updateSelectedNodeProps({
      departmentId: deptId,
      departmentName: found?.name || '',
    });
  }

  onRoleSelect(roleId: string) {
    const node = this.selectedNode();
    if (!node) return;
    const found = this.roles.find((r) => r.id === roleId);
    this.updateSelectedNodeProps({
      roleId: roleId,
      roleName: found?.name || '',
    });
  }

  updateSelectedNodeProps(partial: Partial<DesignerNode>) {
    const current = this.selectedNode();
    if (!current) return;

    const updated: DesignerNode = { ...current, ...partial };
    this.selectedNode.set(updated);
    this.nodes.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
    this.emitChange();
  }

  updateSelectedEdgeLabel(label: string) {
    const current = this.selectedEdge();
    if (!current) return;

    const updated: DesignerEdge = { ...current, label };
    this.selectedEdge.set(updated);
    this.edges.update((list) => list.map((e) => (e.id === updated.id ? updated : e)));
    this.emitChange();
  }

  // --- VALIDATION INTEGRATION ---
  setValidationResult(issues: { type: 'error' | 'warning'; message: string; nodeId?: string }[]) {
    const currentNodes = this.nodes();
    const parsed: ValidationIssueItem[] = issues.map((raw, idx) => {
      const msg = raw.message || '';
      let title = 'Lỗi cấu hình quy trình';
      let cause = msg;
      let suggestion = 'Vui lòng kiểm tra lại cấu hình bước và đường liên kết trên sơ đồ.';
      let matchedNodeId = raw.nodeId;
      let nodeName: string | undefined;

      // Extract node name in quotes if present: Bước '...'
      const matchQuote = msg.match(/['"“](.*?)['"”]/);
      if (matchQuote && matchQuote[1]) {
        const found = currentNodes.find(
          (n) => n.name.toLowerCase() === matchQuote[1].toLowerCase() || n.id === matchQuote[1]
        );
        if (found) {
          matchedNodeId = found.id;
          nodeName = found.name;
        }
      }

      const lower = msg.toLowerCase();
      if (lower.includes('bắt đầu') || lower.includes('start')) {
        title = 'Thiếu sự kiện Bắt đầu (START)';
        cause = 'Quy trình chưa xác định điểm kích hoạt đầu tiên để tiếp nhận xử lý.';
        suggestion = 'Kéo thả nút "Bắt đầu" (màu xanh lá) từ bảng công cụ bên trái vào vùng vẽ và nối tới bước xử lý đầu tiên.';
      } else if (lower.includes('kết thúc') || lower.includes('end')) {
        title = 'Thiếu sự kiện Kết thúc (END)';
        cause = 'Quy trình chưa có điểm dừng cuối cùng để hoàn tất hoặc đóng luồng công việc.';
        suggestion = 'Kéo thả ít nhất 1 nút "Kết thúc" (màu đỏ) từ bảng công cụ bên trái vào vùng vẽ và nối từ bước cuối cùng đến nút này.';
      } else if (lower.includes('phòng ban') || lower.includes('department')) {
        title = `Chưa gán phòng ban xử lý${nodeName ? ': ' + nodeName : ''}`;
        cause = `Bước xử lý "${nodeName || 'này'}" chưa được chọn phòng ban chịu trách nhiệm thực thi.`;
        suggestion = 'Bấm vào bước này trên sơ đồ, sau đó mở bảng Thuộc tính bên phải và chọn một Phòng ban hợp lệ trong doanh nghiệp.';
      } else if (lower.includes('không thể tiếp cận') || lower.includes('kết nối từ sự kiện bắt đầu') || lower.includes('unreachable')) {
        title = `Bước bị cô lập${nodeName ? ': ' + nodeName : ''}`;
        cause = `Không có đường liên kết nào từ sự kiện Bắt đầu dẫn tới bước "${nodeName || 'này'}".`;
        suggestion = 'Kéo một đường nối từ cổng ra của bước liền trước (hoặc từ điểm Bắt đầu) đến cổng vào của bước này.';
      } else if (lower.includes('đường dẫn đến sự kiện kết thúc') || lower.includes('dead end') || lower.includes('không có lối ra')) {
        title = `Đường cụt - Không thể hoàn tất${nodeName ? ': ' + nodeName : ''}`;
        cause = `Luồng công việc đi vào bước "${nodeName || 'này'}" nhưng không có lối thoát để đi tiếp đến điểm Kết thúc.`;
        suggestion = 'Kéo một đường nối từ cổng ra của bước này đến bước kế tiếp hoặc trực tiếp đến điểm Kết thúc.';
      } else if (lower.includes('chu trình') || lower.includes('vòng lặp') || lower.includes('cycle')) {
        title = 'Phát hiện vòng lặp vô tận (Cycle)';
        cause = 'Các bước nối vòng quay lại nhau mà không có nhánh rẽ để thoát ra sự kiện Kết thúc.';
        suggestion = 'Thêm một Cổng rẽ nhánh điều kiện hoặc điều chỉnh lại các đường nối để đảm bảo có nhánh dẫn đến sự kiện Kết thúc.';
      } else if (lower.includes('cổng rẽ nhánh') || lower.includes('gateway') || lower.includes('nhánh rẽ')) {
        title = 'Cổng điều kiện chưa đủ nhánh rẽ';
        cause = 'Cổng rẽ nhánh yêu cầu ít nhất 2 đường rẽ để phân luồng xử lý theo điều kiện.';
        suggestion = 'Kéo thêm đường nối từ cổng này tới các bước xử lý tương ứng và đặt nhãn/điều kiện cho từng nhánh.';
      }

      return {
        id: `issue_${idx}_${Date.now()}`,
        type: raw.type,
        title,
        cause,
        suggestion,
        nodeId: matchedNodeId,
        nodeName: nodeName || (matchedNodeId ? currentNodes.find((n) => n.id === matchedNodeId)?.name : undefined),
      };
    });

    this.validationIssues.set(parsed);
    this.showValidation.set(true);
  }

  focusIssueNode(nodeId?: string) {
    if (!nodeId) return;
    const target = this.nodes().find((n) => n.id === nodeId);
    if (target) {
      this.selectNode(target);
      // center canvas smoothly on this node
      const rect = this.canvasContainer?.nativeElement.getBoundingClientRect();
      if (rect) {
        this.panX.set(Math.round(rect.width / 2 - target.x * this.zoom()));
        this.panY.set(Math.round(rect.height / 2 - target.y * this.zoom()));
      }
    }
  }
}
