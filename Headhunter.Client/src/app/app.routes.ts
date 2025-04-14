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
    {
        path: 'voters',
        loadComponent: () => import('./voters/voters.component').then(c => c.VotersComponent),
        title: 'Voters',
    },
];
