<script setup>
import { reactive, watch } from 'vue'
const props = defineProps({ project: Object, submitting: Boolean }); const emit = defineEmits(['submit']); const form = reactive({ name: '', description: '', startDate: '', endDate: '' })
watch(() => props.project, value => Object.assign(form, { name: value?.name || '', description: value?.description || '', startDate: value?.startDate || '', endDate: value?.endDate || '' }), { immediate: true })
function submit() { if (form.endDate && form.startDate && form.endDate < form.startDate) return; emit('submit', { ...form }) }
</script>
<template><form class="admin-form" @submit.prevent="submit"><label>Nombre<input v-model="form.name" required maxlength="200" /></label><label>Descripción<textarea v-model="form.description" maxlength="2000" /></label><div class="form-row"><label>Fecha de inicio<input v-model="form.startDate" type="date" /></label><label>Fecha de fin<input v-model="form.endDate" type="date" :min="form.startDate || undefined" /></label></div><slot name="actions" :form="form"><button class="button primary" :disabled="submitting">{{ submitting ? 'Guardando…' : 'Guardar proyecto' }}</button></slot></form></template>
