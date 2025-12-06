/**
 * Checkpoint data structure for managing conversation forks.
 */
export interface CheckpointData {
  checkpointId: string;
  messageIndex: number;
  parentCheckpointId: string | null;
  branchName: string | null;
}

/**
 * Utility functions for conversation branching and checkpoint management.
 * These encapsulate logic for selecting checkpoints for forking and editing.
 */

/**
 * Selects the best checkpoint for editing a message at the given index.
 *
 * When editing a message, we need to fork from the checkpoint that has all
 * messages UP TO (but not including) the message being edited. This allows
 * the user to replace that message with a new one.
 *
 * @param checkpoints List of available checkpoints, ordered by creation
 * @param messageIndex The index of the message being edited (0-based)
 * @returns The checkpoint to fork from, or null if no suitable checkpoint exists
 *
 * @example
 * // Messages: ["Hi", "Hello", "How are you?"]
 * // Edit message at index 1 ("Hello")
 * // Need checkpoint with messageIndex <= 1 (has 0-1 messages: "Hi", "Hello")
 * const cp = selectCheckpointForEdit(checkpoints, 1);
 * // cp.messageIndex should be 1, representing state after 1st message
 */
export function selectCheckpointForEdit(
  checkpoints: CheckpointData[],
  messageIndex: number
): CheckpointData | null {
  // Sort by message index descending to find the highest valid checkpoint
  const sorted = [...checkpoints].sort((a, b) => b.messageIndex - a.messageIndex);

  // Find checkpoint with messageIndex <= target index
  // This gives us the state BEFORE the message being edited
  const checkpoint = sorted.find((cp) => cp.messageIndex <= messageIndex);

  return checkpoint ?? null;
}

/**
 * Finds all checkpoint variants at a specific message index.
 * Useful for ChatGPT-style "1 of 3" variant navigation UI.
 *
 * @param checkpoints List of all checkpoints
 * @param messageIndex The message index to find variants for
 * @returns List of checkpoints that have this message index
 */
export function getCheckpointVariantsAtMessage(
  checkpoints: CheckpointData[],
  messageIndex: number
): CheckpointData[] {
  return checkpoints.filter((cp) => cp.messageIndex === messageIndex);
}

/**
 * Gets the checkpoint with the highest message count.
 * Useful for finding the "latest" state in a conversation.
 *
 * @param checkpoints List of checkpoints
 * @returns The checkpoint with the maximum message index
 */
export function getLatestCheckpoint(checkpoints: CheckpointData[]): CheckpointData | null {
  if (checkpoints.length === 0) return null;
  return checkpoints.reduce((max, cp) => (cp.messageIndex > max.messageIndex ? cp : max));
}

/**
 * Gets all checkpoints for a specific branch.
 *
 * @param checkpoints List of all checkpoints
 * @param branchName The name of the branch
 * @returns Checkpoints belonging to this branch
 */
export function getCheckpointsForBranch(
  checkpoints: CheckpointData[],
  branchName: string
): CheckpointData[] {
  return checkpoints.filter((cp) => cp.branchName === branchName);
}

/**
 * Gets the root checkpoint (the initial state of the conversation).
 * Root checkpoints have messageIndex = -1.
 *
 * @param checkpoints List of checkpoints
 * @returns The root checkpoint, or null if not found
 */
export function getRootCheckpoint(checkpoints: CheckpointData[]): CheckpointData | null {
  return checkpoints.find((cp) => cp.messageIndex === -1) ?? null;
}

/**
 * Builds a checkpoint tree structure for visualization.
 * Represents the parent-child relationships between checkpoints.
 */
export interface CheckpointNode {
  checkpoint: CheckpointData;
  children: CheckpointNode[];
  parent: CheckpointNode | null;
}

/**
 * Builds a tree representation of checkpoints.
 * Useful for visualizing the branching structure in the UI.
 *
 * @param checkpoints Flat list of checkpoints
 * @returns Root node of the tree
 */
export function buildCheckpointTree(checkpoints: CheckpointData[]): CheckpointNode | null {
  if (checkpoints.length === 0) return null;

  // Find root
  const root = checkpoints.find((cp) => cp.messageIndex === -1);
  if (!root) return null;

  // Build node map
  const nodeMap = new Map<string, CheckpointNode>();
  for (const cp of checkpoints) {
    nodeMap.set(cp.checkpointId, {
      checkpoint: cp,
      children: [],
      parent: null,
    });
  }

  // Link parents and children
  for (const cp of checkpoints) {
    if (cp.parentCheckpointId) {
      const childNode = nodeMap.get(cp.checkpointId)!;
      const parentNode = nodeMap.get(cp.parentCheckpointId);
      if (parentNode) {
        childNode.parent = parentNode;
        parentNode.children.push(childNode);
      }
    }
  }

  return nodeMap.get(root.checkpointId) ?? null;
}
