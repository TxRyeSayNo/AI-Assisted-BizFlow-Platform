export function roleLabels(roles: readonly string[] | null | undefined): string {
  const labels: Record<string, string> = {
    PLATFORM_ADMIN: 'Quản trị nền tảng',
    COMPANY_ADMIN: 'Quản trị doanh nghiệp',
    DEPARTMENT_MANAGER: 'Trưởng phòng',
    MANAGER: 'Quản lý',
    EMPLOYEE: 'Nhân viên',
  };
  return roles?.map((role) => labels[role] || role).join(', ') || 'Thành viên';
}
