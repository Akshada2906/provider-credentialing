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
    path: '**',
    redirectTo: 'applications'
  }
];
```

---