import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const refreshToken = auth.getRefreshToken();
  const user = auth.getUser();
  if (refreshToken && user) return true;

  router.navigateByUrl('/login');
  return false;
};

