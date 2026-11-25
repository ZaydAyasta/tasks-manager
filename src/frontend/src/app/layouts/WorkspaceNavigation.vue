<script setup>
defineProps({ items: { type: Array, required: true }, unreadCount: { type: Number, default: 0 }, activeSection: String })
defineEmits(['navigate'])

const icons = {
  '/dashboard': 'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
  '/projects': 'M3 7V5h6l2 2h10v13H3z',
  '/my-work': 'M8 5h8M8 3h8v4H8zM6 5H4v16h16V5h-2M8 14l2 2 5-5',
  '/users': 'M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2m17 0v-2a4 4 0 0 0-3-4M14 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0zm3-4a4 4 0 0 1 0 8',
  '/notifications': 'M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
  '/account': 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2m12-14a4 4 0 1 1-8 0 4 4 0 0 1 8 0z'
}
</script>

<template>
  <nav class="workspace-nav" aria-label="Secciones del sistema">
    <RouterLink v-for="item in items" :key="item.to" :to="item.to" class="nav-link" :class="{ 'section-active': activeSection === item.to }" :aria-current="activeSection === item.to ? 'location' : undefined" @click="$emit('navigate')">
      <svg class="workspace-nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path :d="icons[item.to]" /></svg>
      <span class="workspace-nav-copy"><strong>{{ item.label }}</strong><small>{{ item.description }}</small></span>
      <span v-if="item.to === '/notifications' && unreadCount > 0" class="nav-notification-badge" :aria-label="`${unreadCount} sin leer`">{{ unreadCount > 9 ? '9+' : unreadCount }}</span>
    </RouterLink>
  </nav>
</template>
