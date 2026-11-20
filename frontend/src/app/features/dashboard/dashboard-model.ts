/**
 * Shapes returned by the project, personal and team dashboard endpoints. They are read-only
 * projections over the existing Task storage: no new table, column or permission is involved.
 */

export type ProjectSizeCategory = 'LARGE' | 'SMALL';

export interface ProjectSummaryView {
  projectId: string;
  title: string;
  status: string;
  priority: string;
  deadline: string | null;
  ownerUserId: string | null;
  ownerName: string | null;
  ownerDepartmentId: string | null;
  ownerDepartmentName: string | null;
  sizeCategory: ProjectSizeCategory;
  progressPercent: number;
  workItemCount: number;
  completedWorkItemCount: number;
  cancelledWorkItemCount: number;
  overdueWorkItemCount: number;
  unassignedWorkItemCount: number;
  participantCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectDashboardPage {
  items: ProjectSummaryView[];
  page: number;
  pageSize: number;
  total: number;
  generatedAt: string;
}

export interface ProjectWorkItemView {
  taskId: string;
  title: string;
  status: string;
  priority: string;
  deadline: string | null;
  weight: number;
  progressPercent: number;
  checklistTotal: number;
  checklistDone: number;
  assigneeUserId: string | null;
  assigneeName: string | null;
  assigneeDepartmentId: string | null;
  assigneeDepartmentName: string | null;
  isOverdue: boolean;
  updatedAt: string;
}

export interface ProjectParticipantView {
  userId: string | null;
  fullName: string;
  employeeCode: string | null;
  departmentId: string | null;
  departmentName: string | null;
  assignedWorkItems: number;
  completedWorkItems: number;
  inProgressWorkItems: number;
  overdueWorkItems: number;
  responsibilityPercent: number;
  contributionPercent: number;
  currentWorkItemTitle: string | null;
  isSelf: boolean;
}

export interface ProjectDashboardDetailView {
  project: ProjectSummaryView;
  workItems: ProjectWorkItemView[];
  participants: ProjectParticipantView[];
  canSeeAllParticipants: boolean;
  hiddenWorkItemCount: number;
  hiddenParticipantCount: number;
  myResponsibilityPercent: number;
  myContributionPercent: number;
  generatedAt: string;
}

export interface MyWorkItemView {
  workItem: ProjectWorkItemView;
  projectId: string | null;
  projectTitle: string | null;
}

export interface MyWorkDashboardView {
  items: MyWorkItemView[];
  page: number;
  pageSize: number;
  total: number;
  myProgressPercent: number;
  activeWorkItems: number;
  completedWorkItems: number;
  overdueWorkItems: number;
  pendingDepartmentClaims: number;
  byStatus: Record<string, number>;
  projects: ProjectSummaryView[];
  generatedAt: string;
}

export interface TeamMemberView {
  userId: string;
  fullName: string;
  employeeCode: string;
  departmentId: string | null;
  departmentName: string | null;
  status: string;
  totalAssigned: number;
  activeWorkItems: number;
  completedWorkItems: number;
  overdueWorkItems: number;
  avgProgressPercent: number;
  responsibilitySharePercent: number;
}

export interface TeamDepartmentView {
  departmentId: string;
  name: string;
  parentDepartmentId: string | null;
  memberCount: number;
  activeWorkItems: number;
  overdueWorkItems: number;
  avgProgressPercent: number;
}

export interface TeamDashboardView {
  departments: TeamDepartmentView[];
  members: TeamMemberView[];
  scopeDepartmentIds: string[];
  tenantWide: boolean;
  truncated: boolean;
  memberLimit: number;
  generatedAt: string;
}

export interface DepartmentOption {
  departmentId: string;
  name: string;
}

/** Status codes the API emits, mapped onto the badge palette already used by Reports. */
export function statusBadge(status: string): string {
  switch (status) {
    case 'COMPLETED':
    case 'CONFIRMED':
      return 'badge-success';
    case 'OVERDUE':
    case 'REJECTED':
      return 'badge-danger';
    case 'IN_PROGRESS':
    case 'SUBMITTED':
      return 'badge-info';
    case 'CANCELLED':
      return 'badge-neutral';
    default:
      return 'badge-warning';
  }
}

export function progressTone(percent: number): string {
  if (percent >= 100) return 'tone-done';
  if (percent >= 60) return 'tone-good';
  if (percent >= 25) return 'tone-mid';
  return 'tone-low';
}
