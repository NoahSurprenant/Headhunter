import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** One map tile, positioned relative to the point the preview is centred on. */
export interface PreviewTile {
  url: string;
  left: number;
  top: number;
}

const TILE = 256;

/**
 * Where a point falls in Web Mercator at a zoom level, in pixels from the map's top-left corner (the slippy-map
 * tile scheme the /tiles/ layers use).
 */
export function worldPixel(latitude: number, longitude: number, zoom: number): { x: number; y: number } {
  const size = TILE * 2 ** zoom;
  const lat = (latitude * Math.PI) / 180;
  return {
    x: ((longitude + 180) / 360) * size,
    y: ((1 - Math.log(Math.tan(lat) + 1 / Math.cos(lat)) / Math.PI) / 2) * size,
  };
}

/** The tiles around a point, cols x rows, each with its offset from the point in pixels. */
export function previewTiles(latitude: number, longitude: number, zoom: number, cols = 5, rows = 3): PreviewTile[] {
  const { x, y } = worldPixel(latitude, longitude, zoom);
  const centreX = Math.floor(x / TILE);
  const centreY = Math.floor(y / TILE);
  const tiles: PreviewTile[] = [];
  for (let dy = -Math.floor(rows / 2); dy <= Math.floor(rows / 2); dy++) {
    for (let dx = -Math.floor(cols / 2); dx <= Math.floor(cols / 2); dx++) {
      const tx = centreX + dx;
      const ty = centreY + dy;
      tiles.push({
        url: `/tiles/arcgis-imagery/${zoom}/${tx}/${ty}.jpg`,
        left: tx * TILE - x,
        top: ty * TILE - y,
      });
    }
  }
  return tiles;
}

/**
 * A still satellite view around a point, with a marker: the imagery tiles the globe uses, from this site's
 * /tiles/. Cheap where a second WebGL globe would not be.
 */
@Component({
  selector: 'app-map-preview',
  template: `
    <div class="relative overflow-hidden rounded-xl bg-gray-300" [style.height.px]="height()">
      @for (tile of tiles(); track tile.url) {
        <img [src]="tile.url" alt="" width="256" height="256" loading="lazy" draggable="false"
          class="absolute max-w-none select-none"
          [style.left]="'calc(50% + ' + tile.left + 'px)'" [style.top]="'calc(50% + ' + tile.top + 'px)'" />
      }
      <div class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-4 h-4 rounded-full bg-cyan-400 ring-4 ring-white shadow-lg"></div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MapPreviewComponent {
  latitude = input.required<number>();
  longitude = input.required<number>();
  zoom = input(18);
  height = input(240);

  tiles = computed(() => previewTiles(this.latitude(), this.longitude(), this.zoom()));
}
