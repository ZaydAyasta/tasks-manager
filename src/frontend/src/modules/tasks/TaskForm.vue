<script setup>
import { reactive, watch } from 'vue'
const props = defineProps({ task: Object, submitting: Boolean }); const emit = defineEmits(['submit']); const form = reactive({ title: '', description: '', priority: 'Medium', dueDate: '' })
watch(() => props.task, value => Object.assign(form, { title: value?.title || '', description: value?.description || '', priority: value?.priority || 'Medium', dueDate: value?.dueDate ? value.dueDate.slice(0, 10) : '' }), { immediate: true })
</script>
<template><form class="admin-form" @submit.prevent="emit('submit', { ...form })"><label>Título<input v-model="form.title" required maxlength="240" /></label><label>Descripción<textarea v-model="form.description" maxlength="4000" /></label><div class="form-row"><label>Prioridad<select v-model="form.priority"><option value="Low">Baja</option><option value="Medium">Media</option><option value="High">Alta</option><option value="Critical">Crítica</option></select></label><label>Vencimiento<input v-model="form.dueDate" type="date" /></label></div><button class="button primary" :disabled="submitting">{{ submitting ? 'Guardando…' : 'Guardar tarea' }}</button></form></template>
