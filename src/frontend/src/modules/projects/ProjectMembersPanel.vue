<script setup>
import MemberSelector from './MemberSelector.vue'
defineProps({ members: Array, users: Array, canManage: Boolean, submitting: Boolean }); const emit = defineEmits(['add', 'remove'])
</script>
<template><section class="config-panel"><header><div><p class="eyebrow">EQUIPO</p><h2>Miembros del proyecto</h2></div></header><MemberSelector v-if="canManage" :users="users" :members="members" :submitting="submitting" @add="userId => emit('add', userId)" /><div v-if="!members.length" class="inline-empty">No hay colaboradores adicionales.</div><ul class="person-list"><li v-for="member in members" :key="member.userId"><span class="avatar">{{ member.fullName.slice(0, 1) }}</span><div><b>{{ member.fullName }}</b><small>{{ member.email }} · {{ member.role }}</small></div><button v-if="canManage && member.role !== 'Owner'" class="text-button danger" @click="emit('remove', member)">Quitar</button></li></ul></section></template>
