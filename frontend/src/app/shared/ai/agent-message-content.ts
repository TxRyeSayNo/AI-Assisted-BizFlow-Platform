import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';

export interface MessageToken {
  text: string;
  strong?: boolean;
  dateTime?: string;
  zone?: string;
}

export interface MessageBlock {
  kind: 'heading' | 'paragraph' | 'list';
  items: MessageToken[][];
  ordered?: boolean;
}

// Preserve the offset's wall-clock time, rather than silently converting to the viewer's zone.
function readableDate(value: string): MessageToken | null {
  const match =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2}(?:\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/.exec(value);
  if (!match || !Number.isFinite(Date.parse(value))) return null;
  const [, year, month, day, hour, minute, offset] = match;
  const date = new Date(Date.UTC(+year, +month - 1, +day));
  if (date.getUTCMonth() !== +month - 1 || date.getUTCDate() !== +day) return null;
  const weekday = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', timeZone: 'UTC' }).format(
    date,
  );
  return {
    text: `${hour}:${minute} · ${weekday[0].toUpperCase()}${weekday.slice(1)}, ${day}/${month}/${year}`,
    dateTime: value,
    zone: offset === 'Z' ? 'UTC' : `UTC${offset}`,
  };
}

function tokens(text: string, strong = false): MessageToken[] {
  const pattern =
    /\*\*([^*\n]+)\*\*|\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2})/g;
  const result: MessageToken[] = [];
  let end = 0;
  for (const match of text.matchAll(pattern)) {
    if (match.index! > end) result.push({ text: text.slice(end, match.index), strong });
    if (match[1]) result.push(...tokens(match[1], true));
    else result.push({ ...(readableDate(match[0]) ?? { text: match[0] }), strong });
    end = match.index! + match[0].length;
  }
  if (end < text.length) result.push({ text: text.slice(end), strong });
  return result;
}

function labeledTokens(text: string): MessageToken[] {
  // Existing saved conversations also get readable field labels without rewriting their source.
  if (text.startsWith('**')) return tokens(text);
  const label = /^([^:]{1,100}):\s+(.+)$/.exec(text);
  return label
    ? [...tokens(label[1] + ':', true), { text: ' ' }, ...tokens(label[2])]
    : tokens(text);
}

export function parseAgentMessage(text: string): MessageBlock[] {
  const result: MessageBlock[] = [];
  for (const raw of text.replace(/\r\n?/g, '\n').split('\n')) {
    const line = raw.trim();
    if (!line) continue;
    const heading = /^#{1,4}\s+(.+)$/.exec(line);
    if (heading || (line.endsWith(':') && line.length < 100) || /^\*\*[^*]+\*\*:?$/.test(line)) {
      result.push({ kind: 'heading', items: [tokens(heading?.[1] ?? line.replace(/:$/, ''))] });
      continue;
    }
    const bullet = /^(?:[-*•]\s+|\d+[.)]\s+)(.+)$/.exec(line);
    if (bullet) {
      const ordered = /^\d/.test(line);
      const last = result.at(-1);
      if (last?.kind === 'list' && last.ordered === ordered)
        last.items.push(labeledTokens(bullet[1]));
      else result.push({ kind: 'list', ordered, items: [labeledTokens(bullet[1])] });
    } else result.push({ kind: 'paragraph', items: [labeledTokens(line)] });
  }
  return result;
}

@Component({
  selector: 'bf-agent-message-content',
  standalone: true,
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agent-message-content.html',
  styleUrl: './agent-message-content.scss',
})
export class AgentMessageContentComponent {
  readonly text = input.required<string>();
  readonly blocks = computed(() => parseAgentMessage(this.text()));
}
