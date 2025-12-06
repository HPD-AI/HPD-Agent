<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    export let messageIndex: number;
    export let conversationId: string;
    export let apiBase: string;
    export let currentVariantIndex: number = 0;

    const dispatch = createEventDispatcher<{
        selectVariant: { checkpointId: string; messageIndex: number };
    }>();

    interface Variant {
        checkpointId: string;
        messageIndex: number;
        branchName: string | null;
    }

    let variants: Variant[] = [];
    let isLoading = false;
    let hasLoaded = false;

    // Load variants when component mounts or messageIndex changes
    $: if (conversationId && messageIndex >= 0 && !hasLoaded) {
        loadVariants();
    }

    async function loadVariants() {
        if (isLoading) return;

        isLoading = true;
        try {
            const res = await fetch(`${apiBase}/conversations/${conversationId}/variants/${messageIndex}`);
            if (res.ok) {
                variants = await res.json();
                hasLoaded = true;
            }
        } catch (e) {
            console.error('Failed to load variants:', e);
        } finally {
            isLoading = false;
        }
    }

    function selectPrevious() {
        if (currentVariantIndex > 0) {
            const variant = variants[currentVariantIndex - 1];
            currentVariantIndex--;
            dispatch('selectVariant', {
                checkpointId: variant.checkpointId,
                messageIndex: variant.messageIndex
            });
        }
    }

    function selectNext() {
        if (currentVariantIndex < variants.length - 1) {
            const variant = variants[currentVariantIndex + 1];
            currentVariantIndex++;
            dispatch('selectVariant', {
                checkpointId: variant.checkpointId,
                messageIndex: variant.messageIndex
            });
        }
    }

    // Refresh when requested
    export function refresh() {
        hasLoaded = false;
        loadVariants();
    }
</script>

{#if variants.length > 1}
    <div class="flex items-center gap-1 text-xs text-gray-500">
        <button
            onclick={selectPrevious}
            disabled={currentVariantIndex === 0 || isLoading}
            class="p-0.5 hover:bg-gray-100 rounded disabled:opacity-30 disabled:cursor-not-allowed"
            title="Previous variant"
        >
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
            </svg>
        </button>
        <span class="min-w-[2rem] text-center">
            {currentVariantIndex + 1}/{variants.length}
        </span>
        <button
            onclick={selectNext}
            disabled={currentVariantIndex === variants.length - 1 || isLoading}
            class="p-0.5 hover:bg-gray-100 rounded disabled:opacity-30 disabled:cursor-not-allowed"
            title="Next variant"
        >
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
        </button>
    </div>
{/if}
