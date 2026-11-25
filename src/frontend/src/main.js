import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './app/router'
import './shared/styles/main.css'
import './shared/styles/team-release.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
