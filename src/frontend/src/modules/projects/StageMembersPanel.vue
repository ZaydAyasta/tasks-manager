<script setup>
import MemberSelector from './MemberSelector.vue'
defineProps({ stage: Object, members: Array, projectMembers: Array, canManage: Boolean, submitting: Boolean }); const emit = defineEmits(['add', 'remove'])
</script>
<template><section class="stage-members"><h3>{{ stage.name }}</h3><MemberSelector v-if="canManage" :users="projectMembers.map(member => ({ ...member, id: member.userId, isActive: true }))" :members="members.map(member => ({ ...member, userId: member.userId }))" :stage="true" :submitting="submitting" @add="(userId, role) => emit('add', stage, userId, role)" /><ul class="person-list compact"><li v-for="member in members" :key="member.userId"><div><b>{{ member.fullName }}</b><small>{{ member.email }} · {{ member.role }}</small></div><button v-if="canManage" class="text-button danger" @click="emit('remove', stage, member)">Quitar</button></li><li v-if="!members.length" class="muted">Sin miembros en esta etapa.</li></ul></section></template>
