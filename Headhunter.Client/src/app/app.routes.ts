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
    {
        path: 'voter/:id',
        loadComponent: () => import('./voter/voter.component').then(c => c.VoterComponent),
        title: 'Voter',
        data: { displayAddress: true }, // Not sure why this is needed, thought the default value was true
    },
    {
        path: 'address/:id',
        loadComponent: () => import('./address/address.component').then(c => c.AddressComponent),
        title: 'Address',
    },
];
