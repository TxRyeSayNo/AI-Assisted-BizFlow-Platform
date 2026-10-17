import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageSwitchComponent } from './shared/language-switch/language-switch';

@Component({
  imports: [RouterOutlet, LanguageSwitchComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
