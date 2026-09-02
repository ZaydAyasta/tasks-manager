<script setup>
import { reactive, watch } from 'vue'
const props = defineProps({ stage: Object, submitting: Boolean }); const emit = defineEmits(['submit']); const form = reactive({ name: '', description: '' })
watch(() => props.stage, value => Object.assign(form, { name: value?.name || '', description: value?.description || '' }), { immediate: true })
</script>
<template><form class="admin-form" @submit.prevent="emit('submit', { ...form })"><label>Nombre<input v-model="form.name" required maxlength="120" /></label><label>Descripción<textarea v-model="form.description" maxlength="1000" /></label><button class="button primary" :disabled="submitting">{{ submitting ? 'Guardando…' : 'Guardar etapa' }}</button></form></template>
