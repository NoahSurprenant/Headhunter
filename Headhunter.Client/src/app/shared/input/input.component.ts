import { ChangeDetectionStrategy, ChangeDetectorRef, Component, forwardRef, Inject, Injector, input, OnDestroy, signal } from '@angular/core';
import { ControlValueAccessor, FormControl, FormControlDirective, FormControlName, FormGroupDirective, NG_VALUE_ACCESSOR, NgControl, NgModel, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { debounceTime, distinctUntilChanged, Observable, Subscription } from 'rxjs';

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
  suggestions = signal<string[]>([]);
  onSearch = input<(query: string) => Observable<string[]>>();
  show = signal<boolean>(false);

  private subscription?: Subscription;
  private subscriptionToNull?: Subscription;
  private subscriptionAutoComplete?: Subscription;
  private subscriptionAutoCompleteSearch?: Subscription;
  public control!: FormControl;

  constructor(@Inject(Injector) private injector: Injector, private _cdr: ChangeDetectorRef) {
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.subscriptionToNull?.unsubscribe();
    this.subscriptionAutoComplete?.unsubscribe();
    this.subscriptionAutoCompleteSearch?.unsubscribe();
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
    // TODO: fix this? what did I mean by this?
    this.subscriptionToNull?.unsubscribe();
    this.subscriptionToNull = this.control.events.subscribe({
      next: (x) => {
        if (this.convertEmptyToNull() && x.source.value === '')
          this.control.patchValue(null, { emitEvent: false });
      },
    });

    // TODO: Need to validate that events here are firing in correct order, not stomping on each other
    this.subscriptionAutoComplete?.unsubscribe();
    this.subscriptionAutoComplete = this.control.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged()) // Add a delay and avoid duplicate calls
      .subscribe((value: string) => {
        const callback = this.onSearch();
        if (callback) {
          this.subscriptionAutoCompleteSearch?.unsubscribe();
          if (!value)
            return;
          this.subscriptionAutoCompleteSearch = callback(value)
            .subscribe({
              next: (result: string[]) => {
                this.suggestions.set(result);
              },
            });
        }
      });
  }

  selectSuggestion(suggestion: string): void {
    this.control.setValue(suggestion);
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
