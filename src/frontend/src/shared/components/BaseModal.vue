<script setup>
import { nextTick, ref, useId, watch } from 'vue'
const props = defineProps({ modelValue: Boolean, title: String, wide: Boolean }); const emit = defineEmits(['update:modelValue']); const dialog = ref(null)
const titleId = useId()
watch(() => props.modelValue, async open => { await nextTick(); if (open && !dialog.value?.open) dialog.value?.showModal(); if (!open && dialog.value?.open) dialog.value.close() })
function close() { emit('update:modelValue', false) }
</script>
<template><dialog ref="dialog" class="modal" :class="{ wide }" :aria-labelledby="titleId" @close="close" @cancel.prevent="close"><section><header><h2 :id="titleId">{{ title }}</h2><button type="button" class="icon-button" aria-label="Cerrar" @click="close">&times;</button></header><div class="modal-body"><slot /></div><footer v-if="$slots.actions"><slot name="actions" /></footer></section></dialog></template>
