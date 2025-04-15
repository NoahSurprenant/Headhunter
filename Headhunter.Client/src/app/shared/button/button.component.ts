import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'x-button',
  imports: [
    CommonModule,
  ],
  templateUrl: './button.component.html',
  styleUrl: './button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ButtonComponent {
  clicked = output();
  disabled = input<boolean>(false);
  type = input<'submit' | 'reset' | 'button'>('button');

  handleClick() {
    this.clicked.emit();
  }
}
