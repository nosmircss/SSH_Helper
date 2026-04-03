import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { Edge } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

type EditableBranchType = 'if' | 'foreach' | 'while' | 'try' | 'switch' | 'parallel';

interface BranchMetadata {
  branchPath?: string;
  condition?: string;
  caseValue?: string;
}

function isEditableBranchType(value: string | undefined): value is EditableBranchType {
  return value === 'if'
    || value === 'foreach'
    || value === 'while'
    || value === 'try'
    || value === 'switch'
    || value === 'parallel';
}

function parseIndexedBranch(branchPath: string | undefined, prefix: string): number | null {
  if (!branchPath) return null;
  const parts = branchPath.split('/');
  if (parts.length < 2 || parts[0] !== prefix) return null;
  const parsed = Number.parseInt(parts[1] ?? '', 10);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function parseConditionFromLabel(label: unknown): string {
  if (typeof label !== 'string') return '';
  const normalized = label.trim();
  if (!normalized.toLowerCase().startsWith('elif:')) return '';
  return normalized.slice(5).trim();
}

function parseCaseValueFromLabel(label: unknown): string {
  if (typeof label !== 'string') return '';
  const normalized = label.trim();
  if (!normalized.toLowerCase().startsWith('case:')) return '';
  return normalized.slice(5).trim();
}

function getEdgeMetadata(edge: Edge | null): BranchMetadata {
  if (!edge) return {};
  const data = (edge.data ?? {}) as Record<string, unknown>;
  return {
    branchPath: typeof data.branchPath === 'string' ? data.branchPath : undefined,
    condition: typeof data.condition === 'string' ? data.condition : undefined,
    caseValue: typeof data.caseValue === 'string' ? data.caseValue : undefined,
  };
}

function toNonNegativeInteger(value: string, fallback: number): number {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}

function getBranchModeOptions(branchType: EditableBranchType): Array<{ value: string; label: string }> {
  if (branchType === 'if') {
    return [
      { value: 'then', label: 'Then' },
      { value: 'elif', label: 'Elif' },
      { value: 'else', label: 'Else' },
    ];
  }

  if (branchType === 'try') {
    return [
      { value: 'do', label: 'Do' },
      { value: 'catch', label: 'Catch' },
      { value: 'finally', label: 'Finally' },
    ];
  }

  if (branchType === 'switch') {
    return [
      { value: 'case', label: 'Case' },
      { value: 'default', label: 'Default' },
    ];
  }

  if (branchType === 'parallel') {
    return [{ value: 'branch', label: 'Branch' }];
  }

  return [{ value: 'do', label: 'Do' }];
}

export default function EdgeContextMenu() {
  const edgeContextMenu = useFlowStore((s) => s.edgeContextMenu);
  const hideEdgeContextMenu = useFlowStore((s) => s.hideEdgeContextMenu);
  const removeEdges = useFlowStore((s) => s.removeEdges);
  const edges = useFlowStore((s) => s.edges);
  const nodes = useFlowStore((s) => s.nodes);
  const updateEdgeBranchMetadata = useFlowStore((s) => s.updateEdgeBranchMetadata);

  const menuRef = useRef<HTMLDivElement | null>(null);
  const [branchMode, setBranchMode] = useState('');
  const [condition, setCondition] = useState('');
  const [caseValue, setCaseValue] = useState('');
  const [branchIndex, setBranchIndex] = useState('0');

  const edge = useMemo(() => {
    if (!edgeContextMenu) return null;
    return edges.find((candidate) => candidate.id === edgeContextMenu.edgeId) ?? null;
  }, [edgeContextMenu, edges]);

  const sourceBlockType = useMemo(() => {
    if (!edge) return undefined;
    const sourceNode = nodes.find((node) => node.id === edge.source);
    const blockType = (sourceNode?.data as Record<string, unknown> | undefined)?.blockType;
    return typeof blockType === 'string' ? blockType : undefined;
  }, [edge, nodes]);

  const editableBranchType = isEditableBranchType(sourceBlockType) ? sourceBlockType : null;
  const branchMetadata = getEdgeMetadata(edge);

  useEffect(() => {
    if (!edge || !editableBranchType) {
      setBranchMode('');
      setCondition('');
      setCaseValue('');
      setBranchIndex('0');
      return;
    }

    const branchPath = branchMetadata.branchPath ?? '';
    if (editableBranchType === 'if') {
      if (branchPath.startsWith('elif/')) {
        setBranchMode('elif');
        setBranchIndex(String(parseIndexedBranch(branchPath, 'elif') ?? 0));
        setCondition((branchMetadata.condition ?? parseConditionFromLabel(edge.label)).trim());
        setCaseValue('');
        return;
      }

      setBranchMode(branchPath === 'else' ? 'else' : 'then');
      setBranchIndex(String(parseIndexedBranch(branchPath, 'elif') ?? 0));
      setCondition('');
      setCaseValue('');
      return;
    }

    if (editableBranchType === 'try') {
      setBranchMode(branchPath === 'catch' || branchPath === 'finally' ? branchPath : 'do');
      setBranchIndex('0');
      setCondition('');
      setCaseValue('');
      return;
    }

    if (editableBranchType === 'switch') {
      if (branchPath === 'default' || branchPath === 'else') {
        setBranchMode('default');
        setBranchIndex('0');
        setCaseValue('');
      } else {
        setBranchMode('case');
        setBranchIndex(String(parseIndexedBranch(branchPath, 'cases') ?? 0));
        setCaseValue((branchMetadata.caseValue ?? parseCaseValueFromLabel(edge.label)).trim());
      }
      setCondition('');
      return;
    }

    if (editableBranchType === 'parallel') {
      setBranchMode('branch');
      setBranchIndex(String(parseIndexedBranch(branchPath, 'parallel') ?? 0));
      setCondition('');
      setCaseValue('');
      return;
    }

    setBranchMode('do');
    setBranchIndex('0');
    setCondition('');
    setCaseValue('');
  }, [branchMetadata.branchPath, branchMetadata.caseValue, branchMetadata.condition, edge, editableBranchType]);

  const handleClickOutside = useCallback((event: MouseEvent) => {
    const target = event.target as globalThis.Node | null;
    if (menuRef.current && target && menuRef.current.contains(target)) {
      return;
    }

    hideEdgeContextMenu();
  }, [hideEdgeContextMenu]);

  const handleContextMenuOutside = useCallback((event: MouseEvent) => {
    const target = event.target as globalThis.Node | null;
    if (menuRef.current && target && menuRef.current.contains(target)) {
      return;
    }

    hideEdgeContextMenu();
  }, [hideEdgeContextMenu]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        hideEdgeContextMenu();
      }
    },
    [hideEdgeContextMenu],
  );

  useEffect(() => {
    if (!edgeContextMenu) return;

    // Delay listener attachment to avoid closing immediately from the triggering right-click
    const timer = setTimeout(() => {
      document.addEventListener('click', handleClickOutside);
      document.addEventListener('contextmenu', handleContextMenuOutside);
      document.addEventListener('keydown', handleKeyDown);
    }, 0);

    return () => {
      clearTimeout(timer);
      document.removeEventListener('click', handleClickOutside);
      document.removeEventListener('contextmenu', handleContextMenuOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [edgeContextMenu, handleClickOutside, handleContextMenuOutside, handleKeyDown]);

  if (!edgeContextMenu) return null;

  const { x, y, edgeId } = edgeContextMenu;
  const canEditBranchMetadata = editableBranchType !== null && edge !== null;

  const commitBranchMetadata = () => {
    if (!edge || !editableBranchType) return;

    if (editableBranchType === 'if') {
      if (branchMode === 'else') {
        updateEdgeBranchMetadata(edge.id, { branchPath: 'else', condition: '' });
      } else if (branchMode === 'elif') {
        const index = toNonNegativeInteger(branchIndex, parseIndexedBranch(branchMetadata.branchPath, 'elif') ?? 0);
        updateEdgeBranchMetadata(edge.id, {
          branchPath: `elif/${index}/then`,
          condition: condition.trim(),
        });
      } else {
        updateEdgeBranchMetadata(edge.id, { branchPath: 'then', condition: '' });
      }
      hideEdgeContextMenu();
      return;
    }

    if (editableBranchType === 'try') {
      const normalizedBranch = branchMode === 'catch' || branchMode === 'finally' ? branchMode : 'try';
      updateEdgeBranchMetadata(edge.id, { branchPath: normalizedBranch, condition: '', caseValue: '' });
      hideEdgeContextMenu();
      return;
    }

    if (editableBranchType === 'switch') {
      if (branchMode === 'default') {
        updateEdgeBranchMetadata(edge.id, { branchPath: 'default', condition: '', caseValue: '' });
      } else {
        const index = toNonNegativeInteger(branchIndex, parseIndexedBranch(branchMetadata.branchPath, 'cases') ?? 0);
        updateEdgeBranchMetadata(edge.id, {
          branchPath: `cases/${index}/do`,
          caseValue: caseValue.trim(),
          condition: '',
        });
      }
      hideEdgeContextMenu();
      return;
    }

    if (editableBranchType === 'parallel') {
      const index = toNonNegativeInteger(branchIndex, parseIndexedBranch(branchMetadata.branchPath, 'parallel') ?? 0);
      updateEdgeBranchMetadata(edge.id, {
        branchPath: `parallel/${index}`,
        condition: '',
        caseValue: '',
      });
      hideEdgeContextMenu();
      return;
    }

    updateEdgeBranchMetadata(edge.id, { branchPath: 'do', condition: '', caseValue: '' });
    hideEdgeContextMenu();
  };

  return (
    <div
      ref={menuRef}
      style={{
        position: 'fixed',
        left: x,
        top: y,
        zIndex: 50,
        background: '#12122a',
        border: '1px solid #2a2a4a',
        borderRadius: 6,
        padding: '4px 0',
        minWidth: 220,
        boxShadow: '0 6px 20px rgba(0, 0, 0, 0.5)',
      }}
    >
      {canEditBranchMetadata && (
        <div style={{ padding: '8px 12px', borderBottom: '1px solid #2a2a4a', display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label style={{ fontSize: 11, color: '#888' }}>Branch</label>
          <select
            data-testid="edge-branch-mode-input"
            value={branchMode}
            onChange={(e) => setBranchMode(e.target.value)}
            style={{
              width: '100%',
              padding: '4px 6px',
              background: '#0d1117',
              border: '1px solid #2a2a4a',
              borderRadius: 4,
              color: '#ddd',
              fontSize: 12,
            }}
          >
            {getBranchModeOptions(editableBranchType).map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          {((editableBranchType === 'if' && branchMode === 'elif')
            || (editableBranchType === 'switch' && branchMode === 'case')
            || editableBranchType === 'parallel') && (
            <>
              <label style={{ fontSize: 11, color: '#888' }}>Branch Index</label>
              <input
                data-testid="edge-branch-index-input"
                type="number"
                min={0}
                value={branchIndex}
                onChange={(e) => setBranchIndex(e.target.value)}
                style={{
                  width: '100%',
                  padding: '4px 6px',
                  background: '#0d1117',
                  border: '1px solid #2a2a4a',
                  borderRadius: 4,
                  color: '#ddd',
                  fontSize: 12,
                }}
              />
            </>
          )}

          {editableBranchType === 'if' && branchMode === 'elif' && (
            <>
              <label style={{ fontSize: 11, color: '#888' }}>Elif Condition</label>
              <input
                data-testid="edge-branch-condition-input"
                type="text"
                value={condition}
                onChange={(e) => setCondition(e.target.value)}
                placeholder="Expression"
                style={{
                  width: '100%',
                  padding: '4px 6px',
                  background: '#0d1117',
                  border: '1px solid #2a2a4a',
                  borderRadius: 4,
                  color: '#ddd',
                  fontSize: 12,
                  fontFamily: 'monospace',
                }}
              />
            </>
          )}

          {editableBranchType === 'switch' && branchMode === 'case' && (
            <>
              <label style={{ fontSize: 11, color: '#888' }}>Case Value</label>
              <input
                data-testid="edge-branch-case-value-input"
                type="text"
                value={caseValue}
                onChange={(e) => setCaseValue(e.target.value)}
                placeholder="value"
                style={{
                  width: '100%',
                  padding: '4px 6px',
                  background: '#0d1117',
                  border: '1px solid #2a2a4a',
                  borderRadius: 4,
                  color: '#ddd',
                  fontSize: 12,
                  fontFamily: 'monospace',
                }}
              />
            </>
          )}

          <button
            data-testid="edge-branch-save-btn"
            onClick={commitBranchMetadata}
            style={{
              marginTop: 2,
              padding: '5px 8px',
              background: '#1e2f55',
              border: '1px solid #3d5b96',
              borderRadius: 4,
              color: '#cfe1ff',
              fontSize: 12,
              cursor: 'pointer',
            }}
          >
            Save Branch
          </button>
        </div>
      )}

      <button
        onClick={() => {
          removeEdges([edgeId]);
          hideEdgeContextMenu();
        }}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          width: '100%',
          padding: '6px 12px',
          background: 'none',
          border: 'none',
          color: '#e74c3c',
          fontSize: 12,
          cursor: 'pointer',
          textAlign: 'left',
          transition: 'background 0.1s',
        }}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLElement).style.background = '#1e1e3a';
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLElement).style.background = 'none';
        }}
      >
        <span style={{ fontSize: 14, width: 20, textAlign: 'center' }}>
          {'\u2702'}
        </span>
        <span>Delete Connection</span>
      </button>
    </div>
  );
}
