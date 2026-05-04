import { Routes } from '@angular/router';
import { authGuard } from './services/auth.guard';
import { LoginPageComponent } from './pages/login/login-page.component';
import { DashboardPageComponent } from './pages/dashboard/dashboard-page.component';

export const routes: Routes = [
  { path: 'login', component: LoginPageComponent },
  { path: '', component: DashboardPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
