import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ButtonComponent } from './button.component';

@Component({
  imports: [ButtonComponent],
  template: `<x-button [disabled]="disabled()" [type]="type()" (clicked)="clicks = clicks + 1">Go</x-button>`,
})
class HostComponent {
  disabled = signal(false);
  type = signal<'submit' | 'reset' | 'button'>('button');
  clicks = 0;
}

describe('ButtonComponent', () => {
  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    return { fixture, host: fixture.componentInstance, button };
  }

  it('projects its content and emits clicked', () => {
    const { host, button } = render();
    expect(button.textContent!.trim()).toBe('Go');
    button.click();
    button.click();
    expect(host.clicks).toBe(2);
  });

  it('defaults to type="button" and forwards the type input', () => {
    const { fixture, host, button } = render();
    expect(button.type).toBe('button');
    host.type.set('submit');
    fixture.detectChanges();
    expect(button.type).toBe('submit');
  });

  it('is disabled, greyed out and does not emit while disabled', () => {
    const { fixture, host, button } = render();
    host.disabled.set(true);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.classList).toContain('grayscale-80');
    button.click();
    expect(host.clicks).toBe(0);
  });
});
