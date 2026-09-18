import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'applications',
    pathMatch: 'full'
  },
  {
    path: 'applications',
    loadComponent: () => 
      import('./features/applications/pages/application-list/application-list.component')
        .then(m => m.ApplicationListComponent)
  },
  {
    path: 'applications/:id',
    loadComponent: () =>
      import('./features/applications/pages/application-detail/application-detail.component')
        .then(m => m.ApplicationDetailComponent)
  },
  {
    path: '**',
    redirectTo: 'applications'
  }
];