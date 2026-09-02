<script setup>
import { computed, ref } from 'vue'
import { availableMembers } from '../../app/services/admin-ui'
const props = defineProps({ users: { type: Array, default: () => [] }, members: { type: Array, default: () => [] }, submitting: Boolean, stage: Boolean }); const emit = defineEmits(['add']); const selected = ref(''); const role = ref('Member'); const options = computed(() => availableMembers(props.users, props.members))
function add() { if (!selected.value) return; emit('add', selected.value, role.value); selected.value = '' }
</script>
<template><form class="member-selector" @submit.prevent="add"><select v-model="selected" required><option value="" disabled>Selecciona una persona</option><option v-for="user in options" :key="user.id" :value="user.id">{{ user.fullName }} · {{ user.email }}</option></select><select v-if="stage" v-model="role"><option value="Responsible">Responsable</option><option value="Member">Miembro</option></select><button class="button secondary" :disabled="submitting || !options.length">Agregar</button></form></template>
