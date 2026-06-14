// NAVI Mercador (NPC da loja) — port do protótipo. Pelagem dourada, avental
// marrom, boina musgo, óculos meia-lua, olhos fechados em sorriso ^_^.
// PR 63b.

export function NaviMerchant({ size = 180, style }: { size?: number; style?: React.CSSProperties }) {
  const fur = "#d4a045", shade = "#b8863a", ear = "#f0c98a"
  const apron = "#5a3520", apronHi = "#6e4327", beret = "#5a7340"
  return (
    <svg width={size} height={size * 1.05} viewBox="0 0 200 210" fill="none" style={style}>
      <path d="M150 150 q30 4 30 -26 q0 -18 -16 -18 q9 7 7 21 q-3 16 -22 16 Z" fill={shade} />
      <path d="M58 168 q0 -40 42 -40 q42 0 42 40 l3 42 l-90 0 Z" fill={fur} />
      <path d="M70 150 q30 -10 60 0 l5 60 l-70 0 Z" fill={apron} />
      <path d="M70 150 q18 -6 30 -4 l-2 64 l-30 0 Z" fill={apronHi} opacity="0.5" />
      <rect x="84" y="176" width="32" height="22" rx="4" fill={apronHi} stroke={shade} strokeWidth="1.5" opacity="0.9" />
      <path d="M84 132 l8 18 M116 132 l-8 18" stroke={apron} strokeWidth="4" strokeLinecap="round" />
      <ellipse cx="62" cy="196" rx="15" ry="11" fill={fur} />
      <ellipse cx="138" cy="196" rx="15" ry="11" fill={fur} />
      <g stroke={shade} strokeWidth="1.4" opacity="0.6">
        <path d="M58 198 v6 M62 199 v6 M66 198 v6" />
        <path d="M134 198 v6 M138 199 v6 M142 198 v6" />
      </g>
      <path d="M62 60 L56 24 L94 48 Z" fill={fur} />
      <path d="M138 60 L144 24 L106 48 Z" fill={fur} />
      <path d="M66 54 L63 34 L84 48 Z" fill={ear} />
      <path d="M134 54 L137 34 L116 48 Z" fill={ear} />
      <ellipse cx="100" cy="80" rx="44" ry="40" fill={fur} />
      <path d="M56 90 l-9 6 l10 3 Z" fill={fur} />
      <path d="M144 90 l9 6 l-10 3 Z" fill={fur} />
      <g stroke="#7a5a28" strokeWidth="1.5" strokeLinecap="round" opacity="0.6">
        <path d="M60 90 l-20 -3" /><path d="M60 96 l-19 4" />
        <path d="M140 90 l20 -3" /><path d="M140 96 l19 4" />
      </g>
      <g stroke="#3a2810" strokeWidth="3.4" strokeLinecap="round" fill="none">
        <path d="M76 76 q8 -8 16 0" />
        <path d="M108 76 q8 -8 16 0" />
      </g>
      <g stroke="#fbbf24" strokeWidth="2.6" fill="none">
        <path d="M74 90 a13 11 0 0 0 24 0" />
        <path d="M102 90 a13 11 0 0 0 24 0" />
        <path d="M98 90 q2 -3 4 0" />
        <path d="M74 90 l-9 -2" />
        <path d="M126 90 l9 -2" />
      </g>
      <path d="M95 92 l10 0 l-5 6 Z" fill="#d97f5a" />
      <path d="M90 100 q10 9 20 0" stroke="#3a2810" strokeWidth="2.6" fill="none" strokeLinecap="round" />
      <g transform="rotate(-12 96 44)">
        <ellipse cx="96" cy="44" rx="38" ry="17" fill={beret} />
        <ellipse cx="96" cy="40" rx="38" ry="15" fill="#6b8a4c" />
        <path d="M70 40 q26 -14 52 0" stroke="#4c6334" strokeWidth="2" fill="none" opacity="0.6" />
        <circle cx="100" cy="29" r="4.5" fill="#4c6334" />
      </g>
    </svg>
  )
}
