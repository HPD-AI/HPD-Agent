import { writable, derived, type Readable } from 'svelte/store';

// Types matching the API DTOs
export interface BranchDto {
    name: string;
    headCheckpointId: string;
    messageCount: number;
    createdAt: string;
}

export interface BranchTreeDto {
    threadId: string;
    rootCheckpointId: string | null;
    nodes: Record<string, BranchNodeDto>;
    namedBranches: Record<string, BranchMetadataDto>;
    activeBranch: string | null;
}

export interface BranchNodeDto {
    checkpointId: string;
    parentCheckpointId: string | null;
    messageCount: number;
    childCheckpointIds: string[];
    branchName: string | null;
}

export interface BranchMetadataDto {
    name: string;
    headCheckpointId: string;
    createdAt: string;
}

export interface BranchState {
    branches: BranchDto[];
    activeBranch: string | null;
    tree: BranchTreeDto | null;
    isLoading: boolean;
    error: string | null;
}

function createBranchStore() {
    const initialState: BranchState = {
        branches: [],
        activeBranch: null,
        tree: null,
        isLoading: false,
        error: null
    };

    const { subscribe, set, update } = writable<BranchState>(initialState);

    return {
        subscribe,

        // Set branches list
        setBranches(branches: BranchDto[]) {
            update(state => ({ ...state, branches, error: null }));
        },

        // Set active branch
        setActiveBranch(branchName: string | null) {
            update(state => ({ ...state, activeBranch: branchName }));
        },

        // Set the full tree
        setTree(tree: BranchTreeDto) {
            update(state => ({
                ...state,
                tree,
                activeBranch: tree.activeBranch,
                branches: Object.entries(tree.namedBranches).map(([name, meta]) => ({
                    name,
                    headCheckpointId: meta.headCheckpointId,
                    messageCount: tree.nodes[meta.headCheckpointId]?.messageCount ?? 0,
                    createdAt: meta.createdAt
                }))
            }));
        },

        // Set loading state
        setLoading(isLoading: boolean) {
            update(state => ({ ...state, isLoading }));
        },

        // Set error
        setError(error: string | null) {
            update(state => ({ ...state, error, isLoading: false }));
        },

        // Handle branch created event
        onBranchCreated(branchName: string, checkpointId: string, messageIndex: number) {
            update(state => ({
                ...state,
                branches: [...state.branches, {
                    name: branchName,
                    headCheckpointId: checkpointId,
                    messageCount: messageIndex,
                    createdAt: new Date().toISOString()
                }],
                activeBranch: branchName
            }));
        },

        // Handle branch switched event
        onBranchSwitched(branchName: string | null) {
            update(state => ({ ...state, activeBranch: branchName }));
        },

        // Handle branch deleted event
        onBranchDeleted(branchName: string) {
            update(state => ({
                ...state,
                branches: state.branches.filter(b => b.name !== branchName),
                activeBranch: state.activeBranch === branchName ? null : state.activeBranch
            }));
        },

        // Handle branch renamed event
        onBranchRenamed(oldName: string, newName: string) {
            update(state => ({
                ...state,
                branches: state.branches.map(b =>
                    b.name === oldName ? { ...b, name: newName } : b
                ),
                activeBranch: state.activeBranch === oldName ? newName : state.activeBranch
            }));
        },

        // Reset store
        reset() {
            set(initialState);
        }
    };
}

export const branchStore = createBranchStore();

// Derived store for whether branching is available (has multiple branches)
export const hasBranches: Readable<boolean> = derived(
    branchStore,
    $store => $store.branches.length > 1
);

// Derived store for branch count
export const branchCount: Readable<number> = derived(
    branchStore,
    $store => $store.branches.length
);
