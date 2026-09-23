function LoadingState({ label = 'Loading…' }) {
  return (
    <div className="flex items-center justify-center py-8 px-4" role="status">
      <div className="flex items-center gap-2.5 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] px-4 py-2 text-sm text-[#6B7280]">
        <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-[#E2E4E9] border-t-[#0F9D74]" />
        <span className="font-medium">{label}</span>
      </div>
    </div>
  )
}

export default LoadingState