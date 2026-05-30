// FlowCanvas/src/utils/connectionRules.ts
import type { Connection, Node, Edge } from '@xyflow/react';
import { blockDefMap } from '../blockDefs/registry';

export interface ConnectionVerdict { ok: boolean; reason?: string; }

const START_ID = '__start__';

export function isConnectionAllowed(connection: Connection, nodes: Node[], edges: Edge[]): ConnectionVerdict {
  const { source, target, sourceHandle, targetHandle } = connection;
  if (!source || !target) return { ok: false, reason: 'Incomplete connection.' };

  if (source === target) return { ok: false, reason: 'A block cannot connect to itself.' };
  if (target === START_ID) return { ok: false, reason: 'Nothing can connect into Start.' };

  // Duplicate edge (same source+sourceHandle+target+targetHandle)
  const isDuplicate = edges.some(
    (e) => e.source === source && e.target === target
      && (e.sourceHandle ?? null) === (sourceHandle ?? null)
      && (e.targetHandle ?? null) === (targetHandle ?? null),
  );
  if (isDuplicate) return { ok: false, reason: 'That connection already exists.' };

  // Fan-in: target already has an incoming edge (Wave 1 forbids fan-in; exporter treats >1 incoming as a stop)
  const targetHasIncoming = edges.some((e) => e.target === target);
  if (targetHasIncoming) return { ok: false, reason: 'A block can have only one incoming connection.' };

  // Per-handle uniqueness. Containers keep distinct branch edges that leave via the BOTTOM handle
  // (sourceHandle null/empty) disambiguated by branchPath — so a container may emit MULTIPLE empty-handle
  // edges; a non-container may emit exactly ONE. Named handles ('continue','false', etc.) allow one each.
  const sourceNode = nodes.find((n) => n.id === source);
  const blockType = (sourceNode?.data as Record<string, unknown> | undefined)?.blockType as string | undefined;
  const def = blockType ? blockDefMap.get(blockType) : undefined;
  const isContainer = !!def?.isContainer;
  const handleKey = sourceHandle ?? '';

  const sameHandleEdges = edges.filter(
    (e) => e.source === source && (e.sourceHandle ?? '') === handleKey,
  );
  if (handleKey === '') {
    // Empty/bottom handle: containers may branch multiple times; non-containers exactly once.
    if (!isContainer && sameHandleEdges.length >= 1) {
      return { ok: false, reason: 'This block already has a successor.' };
    }
  } else if (sameHandleEdges.length >= 1) {
    return { ok: false, reason: `This block already has a connection on its "${handleKey}" output.` };
  }

  // Cycle: target can already reach source via existing edges (adding source->target would close a loop).
  if (canReach(target, source, edges)) {
    return { ok: false, reason: 'That connection would create a loop.' };
  }

  return { ok: true };
}

function canReach(from: string, to: string, edges: Edge[]): boolean {
  if (from === to) return true;
  const adjacency = new Map<string, string[]>();
  for (const e of edges) {
    const list = adjacency.get(e.source) ?? [];
    list.push(e.target);
    adjacency.set(e.source, list);
  }
  const seen = new Set<string>();
  const stack = [from];
  while (stack.length) {
    const node = stack.pop()!;
    if (node === to) return true;
    if (seen.has(node)) continue;
    seen.add(node);
    for (const next of adjacency.get(node) ?? []) stack.push(next);
  }
  return false;
}
