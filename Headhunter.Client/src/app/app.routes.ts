import { Routes } from '@angular/router';

export const routes: Routes = [
    {
        path: '',
        redirectTo: '/globe',
        pathMatch: 'full'
    },
    {
        path: 'globe',
        loadComponent: () => import('./cesium/cesium.component').then(c => c.CesiumComponent),
        title: 'Globe',
    },
];
