import { TestBed } from '@angular/core/testing';
import { AgentMessageContentComponent, parseAgentMessage } from './agent-message-content';

describe('AgentMessageContentComponent', () => {
  function render(text: string): HTMLElement {
    TestBed.configureTestingModule({ imports: [AgentMessageContentComponent] });
    const fixture = TestBed.createComponent(AgentMessageContentComponent);
    fixture.componentRef.setInput('text', text);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('renders existing saved facts, questions and an explicit timezone without changing the hour', () => {
    const element = render(
      'Đã ghi nhận:\n- Chủ đề: Báo cáo doanh thu\n- Thời gian: 2026-11-25T09:00:00+07:00\nCần bổ sung:\n- Cuộc họp diễn ra ở đâu?',
    );
    expect(element.querySelectorAll('h4').length).toBe(2);
    expect(element.querySelectorAll('li').length).toBe(3);
    expect(element.querySelector('strong')?.textContent).toBe('Chủ đề:');
    const time = element.querySelector('time')!;
    expect(time.getAttribute('datetime')).toBe('2026-11-25T09:00:00+07:00');
    expect(time.textContent).toContain('09:00 · Thứ Tư, 25/11/2026');
    expect(time.textContent).toContain('UTC+07:00');
  });

  it('supports selective bold, headings and ordered next steps', () => {
    const element = render(
      '### Việc cần ưu tiên\n**Kết luận:** Có hai việc cần xử lý.\n1. Hoàn thành **báo cáo**\n2. Kiểm tra chứng từ',
    );
    expect(element.querySelector('h4')?.textContent).toBe('Việc cần ưu tiên');
    expect(element.querySelectorAll('ol li').length).toBe(2);
    expect(element.querySelector('ol strong')?.textContent).toBe('báo cáo');
    expect(element.textContent).not.toContain('**');
  });

  it('treats HTML and unsafe links as text, never executable markup', () => {
    const element = render(
      '### <img src=x onerror=alert(1)>\n**<script>alert(1)</script>**\n[click](javascript:alert(1))',
    );
    expect(element.querySelector('img, script, a')).toBeNull();
    expect(element.textContent).toContain('<script>alert(1)</script>');
    expect(element.textContent).toContain('javascript:alert(1)');
  });

  it('keeps midnight dates and negative offsets in their original wall-clock timezone', () => {
    const element = render('Thời gian: 2026-11-25T00:15:00-05:00');
    expect(element.querySelector('time')?.textContent).toContain('00:15 · Thứ Tư, 25/11/2026');
    expect(element.querySelector('time')?.textContent).toContain('UTC-05:00');
  });

  it('leaves malformed dates readable as their original text instead of inventing a correction', () => {
    const element = render('Ngày: 2026-02-30T09:00:00+07:00');
    expect(element.querySelector('time')).toBeNull();
    expect(element.textContent).toContain('2026-02-30T09:00:00+07:00');
  });

  it('keeps long content and multiline lists intact', () => {
    const detail = 'Chi tiết cần được giữ đầy đủ. '.repeat(50);
    const blocks = parseAgentMessage(`### Nội dung\n${detail}\n- A\n- B`);
    expect(blocks[1].items[0].map((part) => part.text).join('')).toBe(detail.trim());
    expect(blocks[2].items.length).toBe(2);
  });
});
