import { ChangeDetectionStrategy, ChangeDetectorRef, Component, forwardRef, Inject, Injector, input, OnDestroy } from '@angular/core';
import { ControlValueAccessor, FormControl, FormControlDirective, FormControlName, FormGroupDirective, NG_VALUE_ACCESSOR, NgControl, NgModel, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { Subscription } from 'rxjs';

@Component({
  selector: 'x-input',
  imports: [
    ReactiveFormsModule,
  ],
  templateUrl: './input.component.html',
  styleUrl: './input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => InputComponent),
      multi: true
    }
  ]
})
export class InputComponent implements ControlValueAccessor, OnDestroy {
  type = input<"text" | "tel" | "url" | "number" | "email" | "password">("text");
  placeholder = input<string>('');
  required = input<boolean>(false);
  displayErrors = input<boolean>(true);
  convertEmptyToNull = input<boolean>(false);

  private subscription?: Subscription;
  private subscriptionToNull?: Subscription;
  public control!: FormControl;

  constructor(@Inject(Injector) private injector: Injector, private _cdr: ChangeDetectorRef) {
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.subscriptionToNull?.unsubscribe();
  }

  // Based on https://stackoverflow.com/questions/45755958/how-to-get-formcontrol-instance-from-controlvalueaccessor
  // and https://levelup.gitconnected.com/angular-get-control-in-controlvalueaccessor-b7f09a485fba
  
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
    // TODO: fix this?
    this.subscriptionToNull?.unsubscribe();
    this.subscriptionToNull = this.control.events.subscribe({
      next: (x) => {
        if (this.convertEmptyToNull() && x.source.value === '')
          this.control.patchValue(null, { emitEvent: false });
      },
    })
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
