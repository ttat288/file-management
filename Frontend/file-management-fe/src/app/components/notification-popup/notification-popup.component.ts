import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type NotificationPopupVariant = 'primary' | 'danger' | 'secondary';

@Component({
  selector: 'app-notification-popup',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notification-popup.component.html',
  styles: [
    `
      :host {
        position: fixed;
        inset: 0;
        z-index: 60;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1rem;
      }
      .overlay {
        position: absolute;
        inset: 0;
        background: rgba(0, 0, 0, 0.25);
        backdrop-filter: blur(4px);
      }
      :host-context(.dark) .overlay {
        background: rgba(0, 0, 0, 0.6);
      }
      :host-context(.light) .overlay {
        background: rgba(255, 255, 255, 0.15);
      }
      .panel {
        position: relative;
        width: min(100%, 36rem);
        background: var(--card);
        color: var(--text);
        border-radius: var(--radius-xl);
        box-shadow: var(--shadow-xl);
        overflow: hidden;
        border: 1px solid var(--border);
      }
      .header {
        border-bottom: 1px solid var(--border);
        padding: 1.25rem 1.5rem;
      }
      .title {
        font-size: 1.05rem;
        font-weight: 600;
      }
      .body {
        padding: 1rem 1.5rem;
        color: var(--muted);
        line-height: 1.6;
      }
      .footer {
        display: flex;
        flex-wrap: wrap;
        justify-content: flex-end;
        gap: 0.75rem;
        padding: 1rem 1.5rem;
        background: var(--card);
        border-top: 1px solid var(--border);
      }
      .btn {
        min-width: 5.5rem;
        padding: 0.5rem 1rem;
        border-radius: var(--radius-md);
        font-size: 0.875rem;
        font-weight: 500;
        border: 1px solid var(--border);
        background: var(--card);
        color: var(--text);
        cursor: pointer;
        transition: all 0.15s ease;
      }
      .btn:hover {
        background: rgba(0, 0, 0, 0.05);
      }
      :host-context(.dark) .btn:hover {
        background: rgba(255, 255, 255, 0.05);
      }
      .btn-primary {
        background: var(--primary);
        color: white;
        border-color: var(--primary);
      }
      .btn-primary:hover {
        background: rgb(from var(--primary) r g b / 0.95);
      }
      .btn-danger {
        background: var(--danger);
        color: white;
        border-color: var(--danger);
      }
      .btn-danger:hover {
        background: rgb(from var(--danger) r g b / 0.95);
      }
    `,
  ],
})
export class NotificationPopupComponent {
  @Input() title = 'Notification';
  @Input() message = '';
  @Input() confirmText = 'Agree';
  @Input() cancelText = 'Cancel';
  @Input() showCancel = true;
  @Input() variant: NotificationPopupVariant = 'primary';

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  get confirmButtonClass(): string {
    return this.variant === 'danger'
      ? 'btn btn-danger'
      : this.variant === 'secondary'
        ? 'btn'
        : 'btn btn-primary';
  }
}
