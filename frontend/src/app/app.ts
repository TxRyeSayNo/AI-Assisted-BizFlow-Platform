import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageSwitchComponent } from './shared/language-switch/language-switch';
import { RobotWidgetComponent } from './shared/ai/robot-widget';

@Component({
  imports: [RouterOutlet, LanguageSwitchComponent, RobotWidgetComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
