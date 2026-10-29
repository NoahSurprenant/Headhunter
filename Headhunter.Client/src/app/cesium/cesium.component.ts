import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, OnInit, signal, untracked, viewChild } from '@angular/core';
import { buildModuleUrl,
  Math as CesiumMath, Cartesian3, ProviderViewModel, UrlTemplateImageryProvider, Viewer,
  Cartesian2, ScreenSpaceEventHandler,
  LabelStyle,
  VerticalOrigin,
  Color,
  ScreenSpaceEventType,
  DistanceDisplayCondition,
  defined,
  Entity
} from 'cesium';
import { distinctUntilChanged, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { CustomInfoBoxComponent } from '../custom-info-box/custom-info-box.component';
import { AreaDto, ClusterDto, clusterId, clusterSize, shortCount } from './clusters';

// Every base layer comes from this site's /tiles/ (Headhunter.API, Tiles/), which fetches it from the
// provider: visitors only talk to this site, provider keys stay on the server, and Cloudflare caches the
// tiles.
function tiles(layer: string, extension: 'png' | 'jpg', maximumLevel: number, credit: string) {
  return new UrlTemplateImageryProvider({
    url: `/tiles/${layer}/{z}/{x}/{y}.${extension}`,
    maximumLevel,
    credit,
  });
}

const esriCredit = 'Esri';
const esriImageryCredit = 'Esri, Maxar, Earthstar Geographics, and the GIS User Community';
const esriOceanCredit = 'Esri, GEBCO, NOAA, National Geographic, Garmin, HERE, Geonames.org, and other contributors';
const osmCredit = '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap contributors</a>';

@Component({
  selector: 'app-cesium',
  imports: [
    CustomInfoBoxComponent,
  ],
  templateUrl: './cesium.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './cesium.component.css',
})
export class CesiumComponent implements OnInit {
  viewer!: Viewer;
  private http = inject(HttpClient);
  private searchParams$ = new Subject<{ west: number, east: number, north: number, south: number }>();
  private cancelRequest$ = new Subject<void>();
  // A signal because it is set from Cesium's own DOM callbacks, which Angular
  // (zoneless) does not see; writing the signal schedules change detection.
  selectedEntity = signal<Entity | undefined>(undefined);

  // A place to show: /globe?lat=..&lon=..&address=.. (the voter and address pages link here), or the voters page's
  // selected row. The camera flies there, and the address's info box opens once its point has loaded.
  lat = input(undefined, { transform: optionalNumber });
  lon = input(undefined, { transform: optionalNumber });
  address = input<string | undefined>(undefined);
  private pendingAddress: string | undefined;
  private lastFocus: string | undefined;

  constructor() {
    effect(() => {
      const lat = this.lat();
      const lon = this.lon();
      const address = this.address();
      // Before ngOnInit has made the viewer, it starts there instead.
      if (lat !== undefined && lon !== undefined && this.viewer) {
        untracked(() => this.focus(lat, lon, address));
      }
    });
  }
  trackedEntity = signal<Entity | undefined>(undefined);
  // The clusters on the globe now, by entity id, so a click on one can zoom to it.
  private clusters = new Map<string, ClusterDto>();
  customInfoBox = viewChild<CustomInfoBoxComponent>('customInfoBox');

  async ngOnInit(): Promise<any> {
    // Your access token can be found at: https://ion.cesium.com/tokens.
    // Replace `your_access_token` with your Cesium ion access token.

    //Ion.defaultAccessToken = 'your_access_token';

    const vms = this.createDefaultImageryProviderViewModels();
    const vm = vms.find(x => x.name == 'ArcGIS World Imagery');

    // Initialize the Cesium Viewer in the HTML element with the `cesiumContainer` ID.
    this.viewer = new Viewer('cesiumContainer', {
      geocoder: false, // Search address, cool but requires apikey
      baseLayerPicker: true,
      animation: false, // No need for animation over time
      timeline: false,
      infoBox: false,
      selectedImageryProviderViewModel: vm,
      imageryProviderViewModels: vms,
      terrainProviderViewModels: [],
    });

    // Fly the camera to the place asked for, else to Michigan at the given longitude, latitude, and height.
    const lat = this.lat();
    const lon = this.lon();
    if (lat !== undefined && lon !== undefined) {
      this.focus(lat, lon, this.address());
    } else {
      this.viewer.camera.flyTo({
        destination: Cartesian3.fromDegrees(-84.557916, 43.333202, 1000000),
        orientation: {
          heading: CesiumMath.toRadians(0.0),
          pitch: CesiumMath.toRadians(-90),
        }
      });
    }

    this.searchParams$
    .pipe(
      distinctUntilChanged((prev, curr) => // Ignore duplicate requests, 
        prev.west === curr.west && // it likes to do this on load before user adjusts camera for some reason
        prev.east === curr.east &&
        prev.north === curr.north &&
        prev.south === curr.south
      ),
      tap(() => this.cancelRequest$.next()), // Cancel existing request
      switchMap((params) => {
        let httpParams = new HttpParams()
          .append('west', params.west)
          .append('east', params.east)
          .append('north', params.north)
          .append('south', params.south);

        return this.http.get<AreaDto>('api/area', { params: httpParams })
          .pipe(takeUntil(this.cancelRequest$));
      })
    )
    .subscribe({
      next: (area) => {
        this.viewer.entities.suspendEvents();

        // Zoomed out, the API sends clusters (a count per grid cell) instead of addresses.
        this.clusters = new Map(area.clusters.map(c => [clusterId(c), c]));

        // Only remove entities that are not in the new result set
        const all = this.viewer.entities.values.map(x => x.id);
        const current = [...area.addresses.map(x => x.id), ...this.clusters.keys()];
        const remove = all.filter(x => !current.includes(x));
        remove.forEach(x => {
          this.viewer.entities.removeById(x);
        });

        const busiest = Math.max(0, ...area.clusters.map(c => c.count));
        this.clusters.forEach((c, id) => {
          if (this.viewer.entities.getById(id)) {
            return;
          }
          this.viewer.entities.add({
            id,
            name: c.count.toLocaleString() + ' addresses',
            position: Cartesian3.fromDegrees(c.longitude, c.latitude),
            point: {
              pixelSize: clusterSize(c.count, busiest),
              color: Color.ORANGE.withAlpha(0.75),
              outlineColor: Color.WHITE,
              outlineWidth: 1,
            },
            // A lone address needs no number on it.
            label: c.count <= 1 ? undefined : {
              text: shortCount(c.count),
              font: "bold 10pt sans-serif",
              style: LabelStyle.FILL_AND_OUTLINE,
              outlineWidth: 3,
              verticalOrigin: VerticalOrigin.CENTER,
            },
          });
        });

        area.addresses.forEach(x => {
          const existing = this.viewer.entities.getById(x.id);
          if (existing) {
            // Do any updates here, not really needed for now
            //existing.position = new ConstantPositionProperty(Cartesian3.fromDegrees(long, lat));
            return;
          }

          // Links to the address and voter pages; the info box routes them inside the app.
          let desc = '<div><a href="/address/' + encodeURIComponent(x.id) + '">' + escapeHtml(x.streetNumber + ' ' + x.streetName) + '</a>, '
            + escapeHtml(x.city + ' ' + x.state + ' ' + x.zipCode) + '</div>';
          x.voters.forEach(voter => {
            desc += '<div><a href="/voter/' + encodeURIComponent(voter.id) + '">' + escapeHtml(voter.firstName + ' ' + voter.lastName) + '</a></div>';
          });

          this.viewer.entities.add({
            id: x.id,
            name: x.streetNumber + ' ' + x.streetName,
            description: desc,
            position: Cartesian3.fromDegrees(x.longitude, x.latitude),
            point: {
              pixelSize: 5,
              color: Color.AQUA,
              outlineColor: Color.WHITE,
              outlineWidth: 2,
            },
            label: {
              text: x.streetNumber + ' ' + x.streetName,
              font: "10pt monospace",
              style: LabelStyle.FILL_AND_OUTLINE,
              outlineWidth: 2,
              verticalOrigin: VerticalOrigin.TOP,
              pixelOffset: new Cartesian2(0, -9),
              // Street numbers only up close: a street of them from higher up is one unreadable line.
              distanceDisplayCondition: new DistanceDisplayCondition(0, 400),
            },
          });
        });
        this.viewer.entities.resumeEvents();

        // The address asked for, now that its point is here: open its info box.
        const pending = this.pendingAddress && this.viewer.entities.getById(this.pendingAddress);
        if (pending) {
          this.pendingAddress = undefined;
          this.selectedEntity.set(pending);
        }
      },
      error: (error) => {
        console.error(error);
      }
    });
    
    this.viewer.camera.moveEnd.addEventListener(this.computeBounds, this);

    const handler = new ScreenSpaceEventHandler(this.viewer.scene.canvas);
    handler.setInputAction((movement: { position: Cartesian2 }) => {
      const pickedObject = this.viewer.scene.pick(movement.position);
      const cluster = defined(pickedObject) && pickedObject.id ? this.clusters.get((pickedObject.id as Entity).id) : undefined;
      if (cluster) {
        // A cluster: zoom in on it, a quarter of the way down, until its addresses are few enough to draw.
        this.viewer.camera.flyTo({
          destination: Cartesian3.fromDegrees(cluster.longitude, cluster.latitude, this.viewer.camera.positionCartographic.height / 4),
        });
      } else if (defined(pickedObject) && pickedObject.id) {
        this.selectedEntity.set(pickedObject.id as Entity);
      } else {
        const cib = this.customInfoBox();
        if (cib) {
          cib.close();
        } else {
          // Just to be safe set to undefined
          this.selectedEntity.set(undefined);
        }
      }
    }, ScreenSpaceEventType.LEFT_CLICK);

    this.viewer.trackedEntityChanged.addEventListener(() => {
      this.trackedEntity.set(this.viewer.trackedEntity);
    });
  }

  closeInfoBox(): void {
    this.selectedEntity.set(undefined);
  }

  /** Straight down on a point, a block or two across (labels show), and the address's info box when it loads. */
  focus(latitude: number, longitude: number, address?: string): void {
    // ngOnInit and the inputs' effect both see the starting place: fly there once.
    const key = latitude + ',' + longitude + ',' + (address ?? '');
    if (key === this.lastFocus) {
      return;
    }
    this.lastFocus = key;
    this.pendingAddress = address;
    this.selectedEntity.set(undefined);
    this.viewer.camera.flyTo({
      destination: Cartesian3.fromDegrees(longitude, latitude, 350),
      orientation: {
        heading: CesiumMath.toRadians(0.0),
        pitch: CesiumMath.toRadians(-90),
      },
    });
  }

  computeBounds() {
    const camera = this.viewer.camera;
    const scene = this.viewer.scene;
    const rectangle = camera.computeViewRectangle(scene.globe.ellipsoid);

    if (rectangle == null)
      return;

    const west = CesiumMath.toDegrees(rectangle.west);
    const south = CesiumMath.toDegrees(rectangle.south);
    const east = CesiumMath.toDegrees(rectangle.east);
    const north = CesiumMath.toDegrees(rectangle.north);

    this.searchParams$.next({ west, east, north, south });
  }

  createDefaultImageryProviderViewModels() {
    const providerViewModels = [];
  
    providerViewModels.push(
      new ProviderViewModel({
        name: "ArcGIS World Imagery",
        iconUrl: buildModuleUrl(
          "Widgets/Images/ImageryProviders/ArcGisMapServiceWorldImagery.png",
        ),
        tooltip:
          "\
  ArcGIS World Imagery provides one meter or better satellite and aerial imagery in many parts of the world and lower \
  resolution satellite imagery worldwide. The map includes 15m TerraColor imagery at small and mid-scales (~1:591M down to ~1:288k) \
  for the world. The map features Maxar imagery at 0.3m resolution for select metropolitan areas around the world, 0.5m \
  resolution across the United States and parts of Western Europe, and 1m resolution imagery across the rest of the world. \
  In addition to commercial sources, the World Imagery map features high-resolution aerial photography contributed by the \
  GIS User Community. This imagery ranges from 0.3m to 0.03m resolution (down to ~1:280 nin select communities). \
  For more information on this map, including the terms of use, visit us online at \n\
  https://www.arcgis.com/home/item.html?id=10df2279f9684e4a9f6a7f08febac2a9",
        creationFunction: function () {
          return tiles('arcgis-imagery', 'jpg', 19, esriImageryCredit);
        },
      }),
    );
  
    providerViewModels.push(
      new ProviderViewModel({
        name: "ArcGIS World Hillshade",
        iconUrl: buildModuleUrl(
          "Widgets/Images/ImageryProviders/ArcGisMapServiceWorldHillshade.png",
        ),
        tooltip:
          "\
  ArcGIS World Hillshade map portrays elevation as an artistic hillshade. This map is designed to be used as a backdrop for topographical, soil, hydro, \
  landcover or other outdoor recreational maps. The map was compiled from a variety of sources from several data providers. \
  The basemap has global coverage down to a scale of ~1:72k. In select areas of the United States and Europe, coverage is available \
  down to ~1:9k. For more information on this map, including the terms of use, visit us online at \n\
  https://www.arcgis.com/home/item.html?id=1b243539f4514b6ba35e7d995890db1d",
        creationFunction: function () {
          return tiles('arcgis-hillshade', 'jpg', 16, esriCredit);
        },
      }),
    );
  
    providerViewModels.push(
      new ProviderViewModel({
        name: "Esri World Ocean",
        iconUrl: buildModuleUrl(
          "Widgets/Images/ImageryProviders/ArcGisMapServiceWorldOcean.png",
        ),
        tooltip:
          "\
  ArcGIS World Ocean map is designed to be used as a base map by marine GIS professionals and as a reference map by anyone interested in ocean data.  \
  The base map features marine bathymetry. Land features include inland waters and roads overlaid on land cover and shaded relief imagery. \
  The map was compiled from a variety of best available sources from several data providers, including General Bathymetric Chart of the Oceans GEBCO_08 Grid, \
  National Oceanic and Atmospheric Administration (NOAA), and National Geographic, Garmin, HERE, Geonames.org, and Esri, and various other contributors. \
  The base map currently provides coverage for the world down to a scale of ~1:577k, and coverage down to 1:72k in US coastal areas, and various other areas. \
  Coverage down to ~ 1:9k is available limited areas based on regional hydrographic survey data. The base map was designed and developed by Esri. \
  For more information on this map, including our terms of use, visit us online at \n\
  https://www.arcgis.com/home/item.html?id=1e126e7520f9466c9ca28b8f28b5e500",
        creationFunction: function () {
          return tiles('esri-ocean', 'jpg', 13, esriOceanCredit);
        },
      }),
    );
  
    providerViewModels.push(
      new ProviderViewModel({
        name: "Open\u00adStreet\u00adMap",
        iconUrl: buildModuleUrl(
          "Widgets/Images/ImageryProviders/openStreetMap.png",
        ),
        tooltip:
          "OpenStreetMap (OSM) is a collaborative project to create a free editable map \
  of the world.\nhttp://www.openstreetmap.org",
        creationFunction: function () {
          return tiles('osm', 'png', 19, osmCredit);
        },
      }),
    );
  
    return providerViewModels;
  }
}

export interface AddressDto
{
  id: string,
  streetNumber: string,
  streetName: string,
  city: string,
  state: string,
  zipCode: string,
  latitude: number,
  longitude: number,
  voters: VoterDto[],
}

export interface VoterDto
{
  id: string,
  firstName: string,
  lastName: string,
}
/** Query parameters arrive as strings; an absent or empty one is no value. */
export function optionalNumber(value: unknown): number | undefined {
  if (value === undefined || value === null || value === '') return undefined;
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}

export function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
}
