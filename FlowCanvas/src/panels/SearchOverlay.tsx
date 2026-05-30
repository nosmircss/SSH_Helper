import { useEffect, useRef, useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

export default function SearchOverlay() {
  const inputRef = useRef<HTMLInputElement>(null);

  const searchVisible = useFlowStore((s) => s.searchVisible);
  const searchQuery = useFlowStore((s) => s.searchQuery);
  const searchResults = useFlowStore((s) => s.searchResults);
  const searchIndex = useFlowStore((s) => s.searchIndex);
  const setSearchQuery = useFlowStore((s) => s.setSearchQuery);
  const nextSearchResult = useFlowStore((s) => s.nextSearchResult);
  const prevSearchResult = useFlowStore((s) => s.prevSearchResult);
  const closeSearch = useFlowStore((s) => s.closeSearch);

  useEffect(() => {
    if (searchVisible && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [searchVisible]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        closeSearch();
      } else if (e.key === 'Enter' && e.shiftKey) {
        e.preventDefault();
        prevSearchResult();
      } else if (e.key === 'Enter') {
        e.preventDefault();
        nextSearchResult();
      }
    },
    [closeSearch, nextSearchResult, prevSearchResult],
  );

  if (!searchVisible) return null;

  const total = searchResults.length;
  const current = total > 0 ? searchIndex + 1 : 0;

  return (
    <div
      style={{
        position: 'absolute',
        top: 8,
        right: 320,
        zIndex: 20,
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        background: 'var(--fc-surface-0)',
        border: '1px solid var(--fc-border)',
        borderRadius: 6,
        padding: '4px 8px',
        boxShadow: 'var(--fc-shadow-sm)',
      }}
    >
      <input
        ref={inputRef}
        type="text"
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Search blocks..."
        style={{
          width: 180,
          padding: '4px 6px',
          background: 'var(--fc-input-bg)',
          border: '1px solid var(--fc-border)',
          borderRadius: 4,
          color: 'var(--fc-text)',
          fontSize: 12,
          outline: 'none',
        }}
      />

      <span
        style={{
          fontSize: 11,
          color: total > 0 ? 'var(--fc-text-secondary)' : 'var(--fc-text-faint)',
          whiteSpace: 'nowrap',
          minWidth: 48,
          textAlign: 'center',
        }}
      >
        {searchQuery ? `${current} of ${total}` : ''}
      </span>

      <button
        onClick={prevSearchResult}
        disabled={total === 0}
        title="Previous (Shift+Enter)"
        style={{
          background: 'none',
          border: '1px solid var(--fc-border)',
          borderRadius: 3,
          color: total > 0 ? 'var(--fc-text)' : 'var(--fc-text-disabled)',
          cursor: total > 0 ? 'pointer' : 'default',
          padding: '2px 6px',
          fontSize: 12,
          lineHeight: 1,
        }}
      >
        &#9650;
      </button>

      <button
        onClick={nextSearchResult}
        disabled={total === 0}
        title="Next (Enter)"
        style={{
          background: 'none',
          border: '1px solid var(--fc-border)',
          borderRadius: 3,
          color: total > 0 ? 'var(--fc-text)' : 'var(--fc-text-disabled)',
          cursor: total > 0 ? 'pointer' : 'default',
          padding: '2px 6px',
          fontSize: 12,
          lineHeight: 1,
        }}
      >
        &#9660;
      </button>

      <button
        onClick={closeSearch}
        title="Close (Escape)"
        style={{
          background: 'none',
          border: 'none',
          color: 'var(--fc-text-muted)',
          cursor: 'pointer',
          padding: '2px 4px',
          fontSize: 14,
          lineHeight: 1,
        }}
      >
        &#10005;
      </button>
    </div>
  );
}
