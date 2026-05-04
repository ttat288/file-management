import { Injectable, NgZone } from '@angular/core';
import { environment } from '../../environments/environment';

export type RealtimeEvent =
  | { type: 'file_created' | 'file_deleted' | 'file_renamed' | 'folder_created' | 'folder_deleted' | 'folder_renamed'; at: string }
  | { type: string; at: string; [k: string]: unknown };

@Injectable({ providedIn: 'root' })
export class EventsService {
  private source: EventSource | null = null;

  constructor(private zone: NgZone) {}

  connect(getAccessToken: () => string | null, onEvent: (e: RealtimeEvent) => void): void {
    this.disconnect();

    const token = getAccessToken();
    if (!token) return;

    const url = `${environment.apiBaseUrl}/events/stream?access_token=${encodeURIComponent(token)}`;
    this.source = new EventSource(url);

    this.source.onmessage = (msg) => {
      this.zone.run(() => {
        try {
          onEvent(JSON.parse(msg.data) as RealtimeEvent);
        } catch {
          onEvent({ type: 'unknown', at: new Date().toISOString() });
        }
      });
    };

    this.source.onerror = () => {
      // Server may close idle connections; dashboard will reconnect on next refresh.
    };
  }

  disconnect(): void {
    this.source?.close();
    this.source = null;
  }
}

