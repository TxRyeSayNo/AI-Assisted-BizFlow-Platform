import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface WorkflowStepNode {
  id: string;
  order: number;
  name: string;
  state: string;
  roleRequired: string;
  slaHours: number;
  description: string;
  transitionsTo: string[];
}

@Component({
  selector: 'bf-workflow-designer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="designer-wrap">
      <div class="designer-toolbar">
        <div>
          <h3 class="designer-title">Visual Workflow Flowchart</h3>
          <p class="designer-sub">Step sequence, role requirements, and state transition pathways</p>
        </div>
        <div class="view-toggles">
          <button
            type="button"
            class="toggle-btn"
            [class.active]="viewMode() === 'visual'"
            (click)="viewMode.set('visual')"
          >
            Visual Graph
          </button>
          <button
            type="button"
            class="toggle-btn"
            [class.active]="viewMode() === 'table'"
            (click)="viewMode.set('table')"
          >
            Transition Table
          </button>
        </div>
      </div>

      <!-- VISUAL GRAPH VIEW -->
      <div *ngIf="viewMode() === 'visual'" class="canvas-container">
        <div class="nodes-flow">
          <div *ngFor="let step of steps(); let idx = index; let last = last" class="step-wrapper">
            <!-- Step Node Card -->
            <div
              class="node-card"
              [class.selected]="selectedStep()?.id === step.id"
              (click)="selectStep(step)"
              tabindex="0"
              role="button"
              [attr.aria-label]="'Inspect step ' + step.name"
            >
              <div class="node-badge">Step {{ step.order }}</div>
              <h4 class="node-name">{{ step.name }}</h4>
              <div class="node-meta">
                <span class="role-pill">{{ step.roleRequired }}</span>
                <span class="sla-pill">⏱ {{ step.slaHours }}h</span>
              </div>
            </div>

            <!-- Transition Arrow connector -->
            <div *ngIf="!last" class="flow-connector" aria-hidden="true">
              <svg width="40" height="24" viewBox="0 0 40 24" fill="none">
                <path d="M0 12H34" stroke="#8EC5FF" stroke-width="2" stroke-dasharray="4 2" />
                <path d="M28 6L36 12L28 18" stroke="#2563EB" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </div>
          </div>
        </div>

        <!-- Step Inspector Drawer -->
        <div *ngIf="selectedStep()" class="inspector-drawer">
          <div class="inspector-head">
            <h4>Step Details: {{ selectedStep()!.name }}</h4>
            <button type="button" class="btn-close" (click)="selectedStep.set(null)">✕</button>
          </div>
          <div class="inspector-body">
            <dl class="meta-dl">
              <dt>Assigned State</dt>
              <dd><code>{{ selectedStep()!.state }}</code></dd>
              <dt>Required Authority</dt>
              <dd>{{ selectedStep()!.roleRequired }}</dd>
              <dt>SLA Budget</dt>
              <dd>{{ selectedStep()!.slaHours }} business hours</dd>
              <dt>Valid Transitions</dt>
              <dd>
                <span *ngFor="let t of selectedStep()!.transitionsTo" class="next-chip">
                  → {{ t }}
                </span>
              </dd>
            </dl>
            <p class="step-desc">{{ selectedStep()!.description }}</p>
          </div>
        </div>
      </div>

      <!-- TABLE VIEW -->
      <div *ngIf="viewMode() === 'table'" class="table-view">
        <table class="designer-table">
          <thead>
            <tr>
              <th scope="col">Step #</th>
              <th scope="col">Step Name</th>
              <th scope="col">Target State</th>
              <th scope="col">Role Required</th>
              <th scope="col">SLA Budget</th>
              <th scope="col">Permitted Transitions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let s of steps()">
              <td><strong>{{ s.order }}</strong></td>
              <td>{{ s.name }}</td>
              <td><code>{{ s.state }}</code></td>
              <td><span class="role-pill">{{ s.roleRequired }}</span></td>
              <td>{{ s.slaHours }}h</td>
              <td>
                <span *ngFor="let t of s.transitionsTo" class="next-chip">
                  → {{ t }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .designer-wrap {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 12px;
      overflow: hidden;
      margin-top: 1.25rem;
    }
    .designer-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.25rem;
      border-bottom: 1px solid #dfe8f2;
      background: #f8fafd;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .designer-title {
      margin: 0;
      font-size: 1.05rem;
      color: #071b33;
    }
    .designer-sub {
      margin: 0.15rem 0 0;
      font-size: 0.78rem;
      color: #63758b;
    }
    .view-toggles {
      display: flex;
      gap: 0.25rem;
      background: #eaf4ff;
      padding: 0.2rem;
      border-radius: 8px;
    }
    .toggle-btn {
      background: none;
      border: none;
      padding: 0.35rem 0.75rem;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 600;
      color: #145da0;
      cursor: pointer;
      &.active {
        background: #ffffff;
        color: #0b2a4a;
        box-shadow: 0 1px 3px rgba(11, 42, 74, 0.1);
      }
    }
    .canvas-container {
      padding: 2rem 1.25rem;
      background: radial-gradient(circle, rgba(142, 197, 255, 0.15) 1px, transparent 1px);
      background-size: 20px 20px;
      overflow-x: auto;
      min-height: 220px;
      position: relative;
    }
    .nodes-flow {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      min-width: max-content;
      padding-bottom: 1rem;
    }
    .step-wrapper {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .node-card {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 10px;
      padding: 1rem 1.15rem;
      width: 13rem;
      box-shadow: 0 4px 12px rgba(11, 42, 74, 0.05);
      cursor: pointer;
      transition: all 180ms ease;
      position: relative;
      overflow: hidden;
      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 8px 20px rgba(37, 99, 235, 0.15);
        border-color: #2563eb;
      }
      &.selected {
        border-color: #2563eb;
        box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2);
        background: #fcfdff;
      }
    }
    .node-badge {
      font-size: 0.7rem;
      font-weight: 700;
      color: #2563eb;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.25rem;
    }
    .node-name {
      margin: 0 0 0.5rem;
      font-size: 0.95rem;
      color: #071b33;
    }
    .node-meta {
      display: flex;
      gap: 0.35rem;
      flex-wrap: wrap;
    }
    .role-pill {
      background: #eaf4ff;
      color: #1e40af;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.7rem;
      font-weight: 600;
    }
    .sla-pill {
      background: #f1f5f9;
      color: #475569;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.7rem;
    }
    .flow-connector {
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .inspector-drawer {
      margin-top: 1.5rem;
      background: #f8fafd;
      border: 1px solid #dfe8f2;
      border-radius: 8px;
      padding: 1rem;
      animation: card-entrance 200ms ease;
    }
    .inspector-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
      h4 { margin: 0; font-size: 0.95rem; color: #0b2a4a; }
    }
    .btn-close {
      background: none;
      border: none;
      font-size: 1rem;
      cursor: pointer;
      color: #63758b;
    }
    .meta-dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.35rem 1rem;
      font-size: 0.8125rem;
      margin: 0 0 0.5rem;
      dt { font-weight: 600; color: #63758b; }
      dd { margin: 0; color: #10243a; }
    }
    .next-chip {
      display: inline-block;
      margin-right: 0.35rem;
      background: #ecfdf5;
      color: #047857;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .step-desc {
      font-size: 0.8125rem;
      color: #475569;
      margin: 0.5rem 0 0;
      border-top: 1px solid #dfe8f2;
      padding-top: 0.5rem;
    }
    .table-view {
      padding: 1rem;
    }
    .designer-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
      th {
        background: #f8fafd;
        text-align: left;
        padding: 0.65rem 0.85rem;
        border-bottom: 1px solid #dfe8f2;
        color: #63758b;
        font-size: 0.75rem;
        text-transform: uppercase;
      }
      td {
        padding: 0.75rem 0.85rem;
        border-bottom: 1px solid #f1f5f9;
      }
    }
  `]
})
export class WorkflowDesigner {
  @Input() businessType: 'TASK' | 'REQUEST' = 'TASK';

  readonly viewMode = signal<'visual' | 'table'>('visual');
  readonly selectedStep = signal<WorkflowStepNode | null>(null);

  readonly steps = signal<WorkflowStepNode[]>([
    {
      id: 'step-1',
      order: 1,
      name: 'Intake & Assignment',
      state: 'DRAFT',
      roleRequired: 'Manager / Author',
      slaHours: 4,
      description: 'Initial work description authoring, priority definition, and performer assignment.',
      transitionsTo: ['ASSIGNED', 'CANCELLED']
    },
    {
      id: 'step-2',
      order: 2,
      name: 'Claim & Acceptance',
      state: 'ASSIGNED',
      roleRequired: 'Performer / Department',
      slaHours: 8,
      description: 'Designated performer acknowledges task ownership or department member claims queue item.',
      transitionsTo: ['ACCEPTED', 'REJECTED']
    },
    {
      id: 'step-3',
      order: 3,
      name: 'Execution & Evidence',
      state: 'IN_PROGRESS',
      roleRequired: 'Accepted Performer',
      slaHours: 36,
      description: 'Performer completes work checklist deliverables and uploads evidence attachments.',
      transitionsTo: ['SUBMITTED', 'OVERDUE']
    },
    {
      id: 'step-4',
      order: 4,
      name: 'Milestone Review',
      state: 'SUBMITTED',
      roleRequired: 'Manager / Approver',
      slaHours: 12,
      description: 'Manager reviews result deliverables against requirements and issues formal confirmation.',
      transitionsTo: ['CONFIRMED', 'IN_PROGRESS']
    },
    {
      id: 'step-5',
      order: 5,
      name: 'Final Completion',
      state: 'COMPLETED',
      roleRequired: 'System',
      slaHours: 0,
      description: 'All milestones confirmed; append-only audit record preserved and work item archived.',
      transitionsTo: ['ARCHIVED']
    }
  ]);

  selectStep(step: WorkflowStepNode): void {
    if (this.selectedStep()?.id === step.id) {
      this.selectedStep.set(null);
    } else {
      this.selectedStep.set(step);
    }
  }
}
