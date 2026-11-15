import { mount } from 'svelte';
import './style.css';
import App from './App.svelte';

const target = document.querySelector<HTMLDivElement>('#app');
if (!target) throw new Error('Missing #app element');

mount(App, { target });
