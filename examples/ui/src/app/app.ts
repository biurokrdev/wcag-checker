import { Component } from '@angular/core';
import { DocumentCheck } from './features/document-check/document-check';

@Component({
  selector: 'app-root',
  imports: [DocumentCheck],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
