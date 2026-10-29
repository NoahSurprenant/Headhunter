import { TestBed } from '@angular/core/testing';
import { MapPreviewComponent, previewTiles, worldPixel } from './map-preview.component';

describe('map preview', () => {
  it('places points in Web Mercator pixels', () => {
    expect(worldPixel(0, 0, 0)).toEqual({ x: 128, y: 128 });
    // The OpenStreetMap wiki's worked example (Slippy map tilenames): Chicago, zoom 10, is tile 262 / 380.
    const chicago = worldPixel(41.85, -87.65, 10);
    expect(Math.floor(chicago.x / 256)).toBe(262);
    expect(Math.floor(chicago.y / 256)).toBe(380);
  });

  it('lays a 5x3 mosaic around the point, which falls inside the centre tile', () => {
    const tiles = previewTiles(42.3314, -83.0458, 18);
    expect(tiles.length).toBe(15);
    const centre = tiles[7];
    expect(centre.url).toBe('/tiles/arcgis-imagery/18/70599/96986.jpg');
    expect(centre.left).toBeLessThanOrEqual(0);
    expect(centre.left).toBeGreaterThan(-256);
    expect(centre.top).toBeLessThanOrEqual(0);
    expect(centre.top).toBeGreaterThan(-256);
    // Neighbours are a tile apart.
    expect(tiles[8].left - centre.left).toBe(256);
    expect(tiles[12].top - centre.top).toBe(256);
  });

  it('renders the tiles and a marker', async () => {
    const fixture = TestBed.createComponent(MapPreviewComponent);
    fixture.componentRef.setInput('latitude', 42.3314);
    fixture.componentRef.setInput('longitude', -83.0458);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('img').length).toBe(15);
    expect(el.querySelector('img')!.getAttribute('src')).toMatch(/^\/tiles\/arcgis-imagery\/18\//);
  });
});
