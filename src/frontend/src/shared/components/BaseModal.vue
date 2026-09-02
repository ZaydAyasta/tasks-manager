<script setup>
import { nextTick, ref, watch } from 'vue'
const props = defineProps({ modelValue: Boolean, title: String, wide: Boolean }); const emit = defineEmits(['update:modelValue']); const dialog = ref(null)
watch(() => props.modelValue, async open => { await nextTick(); if (open && !dialog.value?.open) dialog.value?.showModal(); if (!open && dialog.value?.open) dialog.value.close() })
function close() { emit('update:modelValue', false) }
</script>
<template><dialog ref="dialog" class="modal" :class="{ wide }" @close="close" @cancel.prevent="close"><section><header><h2>{{ title }}</h2><button class="icon-button" aria-label="Cerrar" @click="close">×</button></header><div class="modal-body"><slot /></div><footer v-if="$slots.actions"><slot name="actions" /></footer></section></dialog></template>
