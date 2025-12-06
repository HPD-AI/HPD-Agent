<script lang="ts">
    import { branchStore, hasBranches, type BranchDto } from './branch-store.js';

    export let conversationId: string;
    export let apiBase: string;
    export let onBranchSwitch: (messageCount: number) => void = () => {};

    let isOpen = false;
    let isLoading = false;

    // Subscribe to store
    let branches: BranchDto[] = [];
    let activeBranch: string | null = null;
    let showBranches = false;

    branchStore.subscribe(state => {
        branches = state.branches;
        activeBranch = state.activeBranch;
    });

    hasBranches.subscribe(value => {
        showBranches = value;
    });

    async function loadBranches() {
        if (!conversationId) return;

        branchStore.setLoading(true);
        try {
            const res = await fetch(`${apiBase}/conversations/${conversationId}/branches/tree`);
            if (res.ok) {
                const tree = await res.json();
                branchStore.setTree(tree);
            } else {
                branchStore.setError('Failed to load branches');
            }
        } catch (e) {
            branchStore.setError(`Error: ${e}`);
        }
    }

    async function switchBranch(branchName: string) {
        if (branchName === activeBranch || isLoading) return;

        isLoading = true;
        try {
            const res = await fetch(
                `${apiBase}/conversations/${conversationId}/branches/${encodeURIComponent(branchName)}/switch`,
                { method: 'POST' }
            );

            if (res.ok) {
                const data = await res.json();
                branchStore.onBranchSwitched(data.branchName);
                onBranchSwitch(data.messageCount);
                isOpen = false;
            } else {
                const error = await res.json();
                console.error('Failed to switch branch:', error.message);
            }
        } catch (e) {
            console.error('Error switching branch:', e);
        } finally {
            isLoading = false;
        }
    }

    function toggleDropdown() {
        isOpen = !isOpen;
        if (isOpen) {
            loadBranches();
        }
    }

    // Close dropdown when clicking outside
    function handleClickOutside(event: MouseEvent) {
        const target = event.target as HTMLElement;
        if (!target.closest('.branch-selector')) {
            isOpen = false;
        }
    }

    // Format date for display
    function formatDate(dateStr: string): string {
        const date = new Date(dateStr);
        return date.toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }
</script>

<svelte:window on:click={handleClickOutside} />

{#if showBranches || branches.length > 0}
    <div class="branch-selector relative">
        <button
            onclick={toggleDropdown}
            class="flex items-center gap-2 px-3 py-1.5 text-sm bg-gray-100 hover:bg-gray-200 rounded-md transition-colors"
            title="Switch branch"
        >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M8 7v6m0 0l-2-2m2 2l2-2M16 7v6m0 0l-2-2m2 2l2-2"
                />
            </svg>
            <span class="font-medium">{activeBranch || 'main'}</span>
            <svg class="w-3 h-3 transition-transform {isOpen ? 'rotate-180' : ''}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
        </button>

        {#if isOpen}
            <div class="absolute top-full left-0 mt-1 w-64 bg-white rounded-lg shadow-lg border z-50">
                <div class="p-2 border-b">
                    <p class="text-xs font-semibold text-gray-500 uppercase">Branches</p>
                </div>
                <div class="max-h-64 overflow-y-auto">
                    {#each branches as branch}
                        <button
                            onclick={() => switchBranch(branch.name)}
                            disabled={isLoading}
                            class="w-full px-3 py-2 text-left hover:bg-gray-50 flex items-center justify-between group
                                {branch.name === activeBranch ? 'bg-blue-50' : ''}"
                        >
                            <div class="flex-1 min-w-0">
                                <div class="flex items-center gap-2">
                                    {#if branch.name === activeBranch}
                                        <span class="w-2 h-2 bg-blue-500 rounded-full"></span>
                                    {/if}
                                    <span class="font-medium truncate {branch.name === activeBranch ? 'text-blue-700' : ''}">
                                        {branch.name}
                                    </span>
                                </div>
                                <p class="text-xs text-gray-500 mt-0.5">
                                    {branch.messageCount} messages
                                </p>
                            </div>
                            <span class="text-xs text-gray-400">
                                {formatDate(branch.createdAt)}
                            </span>
                        </button>
                    {/each}
                </div>
                {#if branches.length === 0}
                    <div class="p-4 text-center text-gray-500 text-sm">
                        No branches yet
                    </div>
                {/if}
            </div>
        {/if}
    </div>
{/if}
