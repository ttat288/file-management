import { Injectable } from '@angular/core';

const THEME_KEY = 'fm.theme';
export type ThemeMode = 'light' | 'dark' | 'system';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  getMode(): ThemeMode {
    const raw = localStorage.getItem(THEME_KEY);
    if (raw === 'light' || raw === 'dark' || raw === 'system') return raw;
    return 'system';
  }

  setMode(mode: ThemeMode): void {
    localStorage.setItem(THEME_KEY, mode);
    this.apply(mode);
  }

  apply(mode: ThemeMode = this.getMode()): void {
    const root = document.documentElement;
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    const isDark = mode === 'dark' || (mode === 'system' && prefersDark);
    root.classList.toggle('dark', isDark);
  }
}

