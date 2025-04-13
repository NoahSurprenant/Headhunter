import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CustomInfoBoxComponent } from './custom-info-box.component';

describe('CustomInfoBoxComponent', () => {
  let component: CustomInfoBoxComponent;
  let fixture: ComponentFixture<CustomInfoBoxComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomInfoBoxComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CustomInfoBoxComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
