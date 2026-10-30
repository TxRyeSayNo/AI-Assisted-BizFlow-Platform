import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { LanguageSwitchComponent } from './shared/language-switch/language-switch';
import { RobotWidgetComponent } from './shared/ai/robot-widget';
import { ToastContainerComponent } from './core/toast/toast-container.component';

@Component({
  imports: [
    RouterOutlet,
    LanguageSwitchComponent,
    RobotWidgetComponent,
    ToastContainerComponent,
    TranslatePipe,
  ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
