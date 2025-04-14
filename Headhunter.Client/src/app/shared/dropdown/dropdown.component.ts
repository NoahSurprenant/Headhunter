import { ChangeDetectionStrategy, ChangeDetectorRef, Component, forwardRef, Inject, Injector, input, OnDestroy } from '@angular/core';
import { ControlValueAccessor, FormControl, FormControlDirective, FormControlName, FormGroupDirective, NG_VALUE_ACCESSOR, NgControl, NgModel, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { Subscription } from 'rxjs';

@Component({
  selector: 'x-dropdown',
  imports: [
    ReactiveFormsModule,
  ],
  templateUrl: './dropdown.component.html',
  styleUrl: './dropdown.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
      {
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => DropdownComponent),
        multi: true
      }
    ]
})
export class DropdownComponent<T extends string | number | boolean | null> implements ControlValueAccessor, OnDestroy {
  displayErrors = input<boolean>(true);
  addNullOption = input<boolean>(false);
  options = input.required<T[]>();

  private subscription?: Subscription;
  public control!: FormControl<T>;
  
  constructor(@Inject(Injector) private injector: Injector, private _cdr: ChangeDetectorRef) {
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  private setControl() {
      const injectedControl = this.injector.get(NgControl);
  
      switch (injectedControl.constructor) {
        case NgModel: {
          const ngControl = injectedControl as NgModel;
          this.control = ngControl.control;
  
          this.subscription?.unsubscribe();
          this.subscription = this.control.valueChanges
            .subscribe((value) => {
              if (ngControl.model !== value || ngControl.viewModel !== value) {
                ngControl.viewToModelUpdate(value);
              }
            });
          break;
        }
        case FormControlName: {
          this.control = this.injector.get(FormGroupDirective).getControl(injectedControl as FormControlName);
          break;
        }
        default: {
          this.control = (injectedControl as FormControlDirective).form as FormControl;
          break;
        }
      }
  }
  
  writeValue(obj: any): void {
    this.setControl();
    this._cdr.markForCheck();
  }

  registerOnChange(fn: any): void {
  }
  registerOnTouched(fn: any): void {
  }

  errors(): string {
    if (!this.displayErrors())
      return '';
    //https://stackoverflow.com/questions/40680321/get-all-validation-errors-from-angular-2-formgroup
    const controlErrors: ValidationErrors | null = this.control.errors;
    let str = '';
    if (controlErrors != null) {
      Object.keys(controlErrors).forEach(keyError => {
        //console.log('keyError: ' + keyError + ', err value: ', controlErrors[keyError]);
        if (keyError == 'required') {
        str += 'This field is required. ';
        } else {
        str += keyError += '. ';
        }
      });
    }
    return str;
  }
}
