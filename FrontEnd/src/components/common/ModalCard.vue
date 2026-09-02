<script setup>
import { nextTick, onBeforeUnmount, ref, watch } from "vue";
import { X } from "lucide-vue-next";

const props = defineProps({
  open: { type: Boolean, default: false },
  title: { type: String, default: "" },
  eyebrow: { type: String, default: "" },
});
const emit = defineEmits(["close"]);

const closeButton = ref(null);

function onKeydown(event) {
  if (event.key === "Escape") emit("close");
}

watch(
  () => props.open,
  async (abierto) => {
    if (abierto) {
      document.addEventListener("keydown", onKeydown);
      // El fondo no debe desplazarse mientras el popup está encima.
      document.body.style.overflow = "hidden";
      await nextTick();
      closeButton.value?.focus();
    } else {
      document.removeEventListener("keydown", onKeydown);
      document.body.style.overflow = "";
    }
  },
);

onBeforeUnmount(() => {
  document.removeEventListener("keydown", onKeydown);
  document.body.style.overflow = "";
});
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div v-if="open" class="modal-scrim" @click.self="emit('close')">
        <div class="modal-card" role="dialog" aria-modal="true" :aria-label="title || 'Detalle'">
          <header class="modal-head">
            <div>
              <span v-if="eyebrow" class="modal-eyebrow">{{ eyebrow }}</span>
              <h2>{{ title }}</h2>
            </div>
            <button
              ref="closeButton"
              class="modal-close"
              type="button"
              aria-label="Cerrar"
              @click="emit('close')"
            >
              <X :size="18" />
            </button>
          </header>
          <div class="modal-body"><slot /></div>
          <footer v-if="$slots.footer" class="modal-foot"><slot name="footer" /></footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-scrim {
  position: fixed;
  inset: 0;
  z-index: 90;
  background: rgb(16 24 40 / 0.45);
  display: grid;
  place-items: center;
  padding: 20px;
}
.modal-card {
  width: min(440px, 100%);
  max-height: calc(100vh - 40px);
  background: #fff;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 18px 48px rgb(16 24 40 / 0.24);
}
.modal-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 14px;
  padding: 18px 20px 15px;
  border-bottom: 1px solid var(--border);
}
.modal-eyebrow {
  display: block;
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
  margin-bottom: 4px;
}
.modal-head h2 {
  margin: 0;
  font-size: 16.5px;
  font-weight: 600;
  letter-spacing: -0.01em;
  line-height: 1.25;
}
.modal-close {
  flex: none;
  display: grid;
  place-items: center;
  width: 31px;
  height: 31px;
  border: 1px solid var(--border);
  border-radius: 7px;
  background: #fff;
  color: var(--muted);
}
.modal-close:hover {
  background: #f7f9fa;
  color: #2d3f4a;
}
.modal-close:focus-visible {
  outline: 2px solid var(--primary);
  outline-offset: 2px;
}
.modal-body {
  flex: 1;
  overflow: auto;
  padding: 18px 20px;
}
.modal-foot {
  padding: 12px 20px;
  border-top: 1px solid var(--border);
  background: #fafbfd;
  text-align: right;
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.15s ease;
}
.modal-enter-active .modal-card,
.modal-leave-active .modal-card {
  transition: transform 0.17s cubic-bezier(0.22, 0.8, 0.3, 1);
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-from .modal-card,
.modal-leave-to .modal-card {
  transform: translateY(10px) scale(0.98);
}
@media (prefers-reduced-motion: reduce) {
  .modal-enter-active,
  .modal-leave-active,
  .modal-enter-active .modal-card,
  .modal-leave-active .modal-card {
    transition: none;
  }
}
</style>
