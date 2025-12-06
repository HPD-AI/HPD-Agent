<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import VariantNavigation from './VariantNavigation.svelte';

    export let index: number;
    export let role: 'user' | 'assistant';
    export let content: string;
    export let thinking: string | undefined = undefined;
    export let isLastMessage: boolean = false;
    export let isLoading: boolean = false;
    export let conversationId: string = '';
    export let apiBase: string = '';
    export let showVariants: boolean = true;

    const dispatch = createEventDispatcher<{
        edit: { index: number; newContent: string };
        selectVariant: { checkpointId: string; messageIndex: number };
    }>();

    function handleVariantSelect(event: CustomEvent<{ checkpointId: string; messageIndex: number }>) {
        dispatch('selectVariant', event.detail);
    }

    let isEditing = false;
    let editContent = '';

    function startEdit() {
        if (role !== 'user') return; // Only allow editing user messages
        editContent = content;
        isEditing = true;
    }

    function cancelEdit() {
        isEditing = false;
        editContent = '';
    }

    function submitEdit() {
        if (editContent.trim() && editContent !== content) {
            dispatch('edit', { index, newContent: editContent.trim() });
        }
        isEditing = false;
        editContent = '';
    }

    function handleKeydown(e: KeyboardEvent) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            submitEdit();
        } else if (e.key === 'Escape') {
            cancelEdit();
        }
    }
</script>

<div class="flex {role === 'user' ? 'justify-end' : 'justify-start'} group">
    <div class="max-w-full px-4 py-3 rounded-lg relative {
        role === 'user'
            ? 'bg-blue-500 text-white'
            : 'bg-white border shadow-sm'
    }">
        <div class="text-xs font-medium mb-1 opacity-70 flex items-center justify-between gap-2">
            <div class="flex items-center gap-2">
                <span>{role === 'user' ? 'You' : 'Agent'}</span>
                {#if showVariants && conversationId && apiBase}
                    <VariantNavigation
                        messageIndex={index}
                        {conversationId}
                        {apiBase}
                        on:selectVariant={handleVariantSelect}
                    />
                {/if}
            </div>
            {#if role === 'user' && !isEditing && !isLoading}
                <button
                    onclick={startEdit}
                    class="opacity-0 group-hover:opacity-100 transition-opacity p-1 hover:bg-white/20 rounded"
                    title="Edit message"
                >
                    <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                            d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"
                        />
                    </svg>
                </button>
            {/if}
        </div>

        {#if thinking}
            <div class="text-xs italic opacity-60 mb-2">
                {thinking}
            </div>
        {/if}

        {#if isEditing}
            <div class="min-w-[300px]">
                <textarea
                    bind:value={editContent}
                    onkeydown={handleKeydown}
                    class="w-full p-2 rounded border bg-white text-gray-900 text-sm resize-none"
                    rows="3"
                    autofocus
                ></textarea>
                <div class="flex gap-2 mt-2 justify-end">
                    <button
                        onclick={cancelEdit}
                        class="px-3 py-1 text-xs bg-gray-200 text-gray-700 rounded hover:bg-gray-300"
                    >
                        Cancel
                    </button>
                    <button
                        onclick={submitEdit}
                        disabled={!editContent.trim() || editContent === content}
                        class="px-3 py-1 text-xs bg-white text-blue-600 rounded hover:bg-blue-50 disabled:opacity-50"
                    >
                        Save & Fork
                    </button>
                </div>
                <p class="text-xs opacity-70 mt-1">
                    Editing creates a new branch
                </p>
            </div>
        {:else}
            <div class="whitespace-pre-wrap">{content}</div>
        {/if}

        {#if isLoading && isLastMessage}
            <div class="mt-2 flex items-center text-xs opacity-60">
                <div class="animate-spin rounded-full h-3 w-3 border-b-2 mr-2"></div>
                Thinking...
            </div>
        {/if}
    </div>
</div>
