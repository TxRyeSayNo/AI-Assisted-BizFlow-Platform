import { TestBed } from '@angular/core/testing';
import { WorkflowAgentEditor, WorkflowAgentPolicy } from './workflow-agent-editor';

describe('workflow AI configuration', () => {
  it('starts in human review mode with no implicit access to description or prior results', () => {
    const component = TestBed.createComponent(WorkflowAgentEditor).componentInstance;
    const emit = vi.spyOn(component.valueChange, 'emit');
    component.toggle(true);
    expect(emit).toHaveBeenCalledWith({
      mode: 'REVIEW',
      instructions: '',
      inputFields: [],
      includeDescription: false,
      includePreviousResults: false,
    });
  });
  it('caps selected fields and preserves the remaining policy when editing', () => {
    const component = TestBed.createComponent(WorkflowAgentEditor).componentInstance;
    component.value = {
      mode: 'AUTO',
      instructions: 'Prepare handover',
      inputFields: Array.from({ length: 20 }, (_, i) => `f${i}`),
      includeDescription: false,
      includePreviousResults: true,
    };
    const emitted: WorkflowAgentPolicy[] = [];
    component.valueChange.subscribe((value) => {
      if (value) emitted.push(value);
    });
    component.fieldToggle('secret', true);
    expect(emitted[0].inputFields).toHaveLength(20);
    expect(emitted[0].inputFields).not.toContain('secret');
    expect(emitted[0].includePreviousResults).toBe(true);
    component.readonly = true;
    component.toggle(false);
    component.patch({ mode: 'REVIEW' });
    expect(emitted).toHaveLength(1);
  });
});
