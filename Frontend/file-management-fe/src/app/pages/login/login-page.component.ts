import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService, ThemeMode } from '../../services/theme.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login-page.component.html',
})
export class LoginPageComponent implements OnInit {
  mode: 'login' | 'register' = 'login';

  email = '';
  password = '';
  displayName = '';

  error = '';
  loading = false;

  theme: ThemeMode = 'system';

  constructor(
    private auth: AuthService,
    private router: Router,
    private themeService: ThemeService,
  ) {}

  ngOnInit(): void {
    this.theme = this.themeService.getMode();
    this.themeService.apply(this.theme);
  }

  setTheme(mode: ThemeMode): void {
    this.theme = mode;
    this.themeService.setMode(mode);
  }

  submit(): void {
    this.error = '';
    this.loading = true;

    const run$ =
      this.mode === 'login'
        ? this.auth.login(this.email.trim(), this.password)
        : this.auth.register(this.email.trim(), this.password, this.displayName.trim() || undefined);

    run$.subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/');
      },
      error: (e) => {
        this.loading = false;
        this.error = e?.message || 'Something went wrong';
      },
    });
  }
}

