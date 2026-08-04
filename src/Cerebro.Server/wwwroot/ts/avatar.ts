const AVATAR_COLORS = ["#3fe0d0", "#ffd23f", "#ff9fb2", "#a78bfa"];

export function avatarColor(candidateId: string): string {
  let hash = 0;
  for (let i = 0; i < candidateId.length; i++) {
    hash = (hash * 31 + candidateId.charCodeAt(i)) >>> 0;
  }
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]!;
}

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0]![0]! + parts[1]![0]!).toUpperCase();
  }
  return name.slice(0, 2).toUpperCase();
}
