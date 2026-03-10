import { useState, useCallback, useMemo, useEffect } from "react";

// ============ CARD DEFINITIONS ============
const CARD_DEFS = {
  // Normative Waste (20)
  morning_flush: { name: "Morning Flush", category: "normative", load: 1, count: 4, flavor: "The 7 AM tsunami.", color: "#8B6914" },
  cartoon_turd: { name: "Cartoon Turd", category: "normative", load: 1, count: 4, flavor: "Just a turd. Classic.", color: "#8B6914", icon: "💩" },
  grease_trap: { name: "Grease Trap Haul", category: "normative", load: 2, count: 3, flavor: "FOG — the bane of every operator.", color: "#8B6914" },
  septic_tanker: { name: "Septic Tanker", category: "normative", load: 2, count: 3, flavor: "Smells exactly how you'd imagine.", color: "#8B6914" },
  low_flow: { name: "Low-Flow Day", category: "normative", load: -1, count: 3, flavor: "Holiday weekend. Weirdly quiet.", color: "#8B6914" },
  dilute_influent: { name: "Dilute Influent", category: "normative", load: -1, count: 3, flavor: "Weak tea.", color: "#8B6914" },
  // Operational Response (10)
  increase_was: { name: "Increase WAS Rate", category: "operational", load: -2, count: 2, flavor: "Waste more sludge.", color: "#2E7D32" },
  boost_aeration: { name: "Boost Aeration", category: "operational", load: -1, count: 2, flavor: "Your electric bill weeps.", color: "#2E7D32" },
  chemical_dosing: { name: "Add Chemical Dosing", category: "operational", load: -2, count: 2, flavor: "The expensive fix.", color: "#2E7D32" },
  extend_srt: { name: "Extend SRT", category: "operational", load: 1, count: 2, flavor: "Helps nitrification, risks filaments.", color: "#2E7D32" },
  emergency_bypass: { name: "Emergency Bypass", category: "operational", load: -3, count: 1, flavor: "Stops the bleeding. Triggers regulatory flag.", color: "#2E7D32", special: "bypass" },
  overtime_crew: { name: "Overtime Crew", category: "operational", load: 0, count: 1, flavor: "WILD: Set any stack to 0. Once per game.", color: "#2E7D32", special: "wild" },
  // Population Growth (6)
  new_subdivision: { name: "New Subdivision", category: "population", load: 2, count: 3, flavor: "+1 to future normative cards here.", color: "#E65100", multiplier: 1 },
  mixed_use: { name: "Mixed-Use Dev", category: "population", load: 3, count: 2, flavor: "+1 to future normative cards here.", color: "#E65100", multiplier: 1 },
  mega_dev: { name: "Mega-Development", category: "population", load: 4, count: 1, flavor: "+2 to future normative cards here.", color: "#E65100", multiplier: 2 },
  // Industrial Discharge (6)
  brewery: { name: "Brewery Discharge", category: "industrial", load: 3, count: 2, flavor: "High BOD slug.", color: "#C62828" },
  metal_plating: { name: "Metal Plating Spill", category: "industrial", load: 4, count: 1, flavor: "Toxic. Skip next card-play phase.", color: "#C62828", special: "skip" },
  food_processor: { name: "Food Processor Dump", category: "industrial", load: 3, count: 2, flavor: "+2 extra if Grease Trap on same stack.", color: "#C62828", special: "grease_combo" },
  pharma_flush: { name: "Pharma Flush", category: "industrial", load: 3, count: 1, flavor: "Triggers regulatory scrutiny.", color: "#C62828", special: "pharma" },
  // Weather (6)
  heavy_rain: { name: "Heavy Rain", category: "weather", load: 2, count: 2, flavor: "I&I overwhelms the system.", color: "#1565C0", duration: 1 },
  snowmelt: { name: "Spring Snowmelt", category: "weather", load: 3, count: 1, flavor: "Everything melts at once.", color: "#1565C0", duration: 2 },
  heat_wave: { name: "Heat Wave", category: "weather", load: 1, count: 1, flavor: "Stacks above +3 jump to +5.", color: "#1565C0", duration: 2, special: "heat" },
  cold_snap: { name: "Cold Snap", category: "weather", load: 1, count: 1, flavor: "Ops cards worth 1 less for 3 turns.", color: "#1565C0", duration: 3, special: "cold" },
  drought: { name: "Drought", category: "weather", load: -2, count: 1, flavor: "Industrial cards +2 extra during.", color: "#1565C0", duration: 2, special: "drought" },
  // Regulatory (4)
  tighter_nutrients: { name: "Tighter Nutrients", category: "regulatory", load: 0, count: 1, flavor: "One stack's range narrows to ±3.", color: "#6A1B9A", special: "tighten", duration: 99 },
  pfas_order: { name: "PFAS Order", category: "regulatory", load: 1, count: 1, flavor: "+1 to all stacks. No fix exists.", color: "#6A1B9A", special: "pfas", duration: 99 },
  consent_decree: { name: "Consent Decree", category: "regulatory", load: 0, count: 1, flavor: "Penalties doubled.", color: "#6A1B9A", special: "consent", duration: 99 },
  permit_renewal: { name: "Permit Renewal", category: "regulatory", load: 0, count: 1, flavor: "One stack's range widens to ±5.", color: "#6A1B9A", special: "widen", duration: 99 },
  // Emergency (2)
  subsurface_fire: { name: "Subsurface Fire", category: "emergency", load: 0, count: 1, flavor: "One stack set to +6. No ops for 2 turns.", color: "#212121", special: "fire" },
  main_collapse: { name: "Main Line Collapse", category: "emergency", load: 0, count: 1, flavor: "All load values doubled for 1 turn.", color: "#212121", special: "collapse" },
};

const CAT_ICONS = { normative: "💩", operational: "🔧", population: "🏘️", industrial: "🏭", weather: "🌧️", regulatory: "📋", emergency: "⚠️" };
const CAT_LABELS = { normative: "Waste", operational: "Ops", population: "Growth", industrial: "Industrial", weather: "Weather", regulatory: "Regulatory", emergency: "Emergency" };
const IMMEDIATE_CATS = ["weather", "regulatory", "emergency"];

function buildDeck() {
  const deck = [];
  let id = 0;
  for (const [key, def] of Object.entries(CARD_DEFS)) {
    for (let i = 0; i < def.count; i++) {
      deck.push({ id: id++, defKey: key, ...def });
    }
  }
  return deck;
}

function shuffle(arr) {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

// ============ GAME STATE ============
const INIT_STACK = () => ({ cards: [], loadValue: 0, popMultiplier: 0, rangeMin: -4, rangeMax: 4, fireBlock: 0 });

function initGame() {
  const deck = shuffle(buildDeck());
  const stacks = [INIT_STACK(), INIT_STACK(), INIT_STACK()];
  // Deal 3 starting cards
  for (let i = 0; i < 3; i++) {
    const card = deck.shift();
    stacks[i].cards.push(card);
    stacks[i].loadValue += card.load;
  }
  const hand = deck.splice(0, 5);
  return {
    deck, hand, stacks,
    weatherStack: [], regulatoryStack: [],
    violationsPile: [],
    score: 0, streak: 0, turn: 1,
    phase: "operator", // operator | world | gameover | won
    cardsPlayedThisTurn: 0,
    skipNextOperator: false,
    collapseActive: false,
    consentDecreeActive: false,
    coldSnapTurns: 0,
    droughtActive: false,
    heatWaveActive: false,
    lastPlayedStack: null,
    overtimeUsed: false,
    log: ["Game started. Play 1–3 cards from your hand."],
    needsTargetSelection: null, // for cards that need a target
    tightenedStack: null,
    widenedStack: null,
    pfasApplied: false,
  };
}

// ============ COMPONENTS ============

function Card({ card, onClick, selected, small, faceDown, style }) {
  if (faceDown) {
    return (
      <div style={{ width: small ? 60 : 80, height: small ? 90 : 115, borderRadius: 6, background: "linear-gradient(135deg, #2c3e50, #34495e)", border: "2px solid #1a252f", display: "flex", alignItems: "center", justifyContent: "center", color: "#5a7a8a", fontSize: small ? 18 : 24, fontWeight: "bold", cursor: "default", flexShrink: 0, ...style }}>
        S
      </div>
    );
  }
  const icon = card.icon || CAT_ICONS[card.category] || "?";
  const loadStr = card.load > 0 ? `+${card.load}` : card.load === 0 ? "0" : `${card.load}`;
  const isImmediate = IMMEDIATE_CATS.includes(card.category);

  return (
    <div
      onClick={onClick}
      style={{
        width: small ? 60 : 80, height: small ? 90 : 115, borderRadius: 6,
        background: selected ? "#fff3e0" : "#fff",
        border: `2px solid ${selected ? "#ff9800" : card.color || "#666"}`,
        borderTop: `4px solid ${card.color || "#666"}`,
        cursor: onClick ? "pointer" : "default",
        display: "flex", flexDirection: "column", padding: small ? 3 : 5,
        fontSize: small ? 8 : 10, fontFamily: "monospace",
        position: "relative", flexShrink: 0,
        boxShadow: selected ? "0 0 8px rgba(255,152,0,0.5)" : "0 1px 3px rgba(0,0,0,0.2)",
        transition: "all 0.15s", ...style,
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: small ? 12 : 16 }}>{icon}</span>
        <span style={{ fontSize: small ? 12 : 16, fontWeight: "bold", color: card.load > 0 ? "#c62828" : card.load < 0 ? "#2e7d32" : "#666" }}>{loadStr}</span>
      </div>
      <div style={{ fontWeight: "bold", fontSize: small ? 7 : 9, marginTop: 2, lineHeight: 1.2, minHeight: small ? 16 : 20 }}>{card.name}</div>
      <div style={{ fontSize: small ? 6 : 7.5, color: "#666", marginTop: "auto", lineHeight: 1.2 }}>{card.flavor}</div>
      {isImmediate && <div style={{ position: "absolute", top: 2, right: 2, fontSize: 6, background: "#ff5722", color: "#fff", borderRadius: 3, padding: "1px 3px" }}>AUTO</div>}
      {card.multiplier && <div style={{ position: "absolute", bottom: 2, right: 2, fontSize: 7, background: "#e65100", color: "#fff", borderRadius: 3, padding: "1px 3px" }}>×+{card.multiplier}</div>}
    </div>
  );
}

function StackView({ stack, index, label, onDrop, highlight, rangeLabel }) {
  const inBalance = stack.loadValue >= stack.rangeMin && stack.loadValue <= stack.rangeMax;
  const loadColor = !inBalance ? "#c62828" : Math.abs(stack.loadValue) >= (stack.rangeMax - 1) ? "#e65100" : "#2e7d32";

  return (
    <div
      onClick={onDrop}
      style={{
        flex: 1, minWidth: 100, maxWidth: 160, background: highlight ? "#e3f2fd" : "#fafafa",
        border: `2px solid ${highlight ? "#1565c0" : inBalance ? "#ccc" : "#c62828"}`,
        borderRadius: 8, padding: 6, display: "flex", flexDirection: "column", cursor: highlight ? "pointer" : "default",
        transition: "all 0.15s",
      }}
    >
      <div style={{ textAlign: "center", marginBottom: 4 }}>
        <div style={{ fontSize: 11, fontWeight: "bold", color: "#333" }}>Stack {String.fromCharCode(65 + index)}</div>
        <div style={{ fontSize: 9, color: "#888" }}>{label}</div>
        <div style={{ fontSize: 28, fontWeight: "bold", color: loadColor, lineHeight: 1 }}>
          {stack.loadValue > 0 ? "+" : ""}{stack.loadValue}
        </div>
        <div style={{ fontSize: 8, color: "#999" }}>Range: {stack.rangeMin} to +{stack.rangeMax}</div>
        {stack.popMultiplier > 0 && <div style={{ fontSize: 8, color: "#e65100" }}>Pop bonus: +{stack.popMultiplier}/waste</div>}
        {stack.fireBlock > 0 && <div style={{ fontSize: 8, color: "#c62828" }}>🔥 No ops: {stack.fireBlock}t</div>}
      </div>
      <div style={{ flex: 1, overflowY: "auto", display: "flex", flexDirection: "column", gap: 2, maxHeight: 240, minHeight: 60 }}>
        {stack.cards.map((c, i) => (
          <div key={c.id} style={{ display: "flex", alignItems: "center", gap: 3, padding: "2px 4px", background: "#fff", borderRadius: 3, borderLeft: `3px solid ${c.color}`, fontSize: 8 }}>
            <span>{CAT_ICONS[c.category]}</span>
            <span style={{ flex: 1 }}>{c.name}</span>
            <span style={{ fontWeight: "bold", color: c.load > 0 ? "#c62828" : c.load < 0 ? "#2e7d32" : "#666" }}>
              {c.load > 0 ? "+" : ""}{c.load}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ============ MAIN GAME ============
export default function Sludgeitaire() {
  const [game, setGame] = useState(initGame);
  const [selectedCard, setSelectedCard] = useState(null);

  const industrialInHand = game.hand.filter(c => c.category === "industrial").length;

  function addLog(g, msg) {
    g.log = [...g.log.slice(-15), msg];
  }

  function getEffectiveLoad(card, targetStackIdx, g) {
    let load = card.load;
    // Population multiplier on normative cards
    if (card.category === "normative" && targetStackIdx !== null) {
      load += g.stacks[targetStackIdx].popMultiplier;
    }
    // Cold snap reduces operational cards
    if (card.category === "operational" && g.coldSnapTurns > 0 && card.special !== "wild") {
      load = Math.min(0, load + 1); // reduce effectiveness by 1 (less negative)
    }
    // Drought boosts industrial
    if (card.category === "industrial" && g.droughtActive) {
      load += 2;
    }
    // Food processor + grease combo
    if (card.special === "grease_combo" && targetStackIdx !== null) {
      if (g.stacks[targetStackIdx].cards.some(c => c.defKey === "grease_trap")) {
        load += 2;
      }
    }
    return load;
  }

  function playCardOnStack(g, card, stackIdx) {
    const effectiveLoad = getEffectiveLoad(card, stackIdx, g);
    g.stacks[stackIdx].cards.push({ ...card, effectiveLoad });
    g.stacks[stackIdx].loadValue += effectiveLoad;

    // Population multiplier
    if (card.multiplier) {
      g.stacks[stackIdx].popMultiplier += card.multiplier;
    }

    // Heat wave: stacks above +3 jump to +5
    if (g.heatWaveActive) {
      for (let s of g.stacks) {
        if (s.loadValue > 3 && s.loadValue < 5) s.loadValue = 5;
      }
    }

    // Collapse doubles
    if (g.collapseActive) {
      // Already applied on resolution
    }

    g.lastPlayedStack = stackIdx;
    addLog(g, `Played ${card.name} on Stack ${String.fromCharCode(65 + stackIdx)} (${effectiveLoad > 0 ? "+" : ""}${effectiveLoad})`);

    // Special effects
    if (card.special === "bypass") {
      g.regulatoryStack.push({ ...card, turnsLeft: 99 });
      addLog(g, "⚠️ Emergency Bypass triggered regulatory flag!");
    }
    if (card.special === "skip") {
      g.skipNextOperator = true;
      addLog(g, "☠️ Metal Plating toxicity — skip next operator phase!");
    }
    if (card.special === "pharma") {
      // add +1 to regulatory pressure - we'll just log it
      addLog(g, "📋 Pharma flush triggered regulatory scrutiny.");
    }
    if (card.special === "wild") {
      g.stacks[stackIdx].loadValue = 0;
      g.overtimeUsed = true;
      addLog(g, "🔧 Overtime Crew reset Stack ${String.fromCharCode(65 + stackIdx)} to 0!");
    }
  }

  function resolveWeather(g, card) {
    g.weatherStack.push({ ...card, turnsLeft: card.duration });
    const loadPerStack = card.load;
    for (let i = 0; i < 3; i++) {
      g.stacks[i].loadValue += loadPerStack;
    }
    if (card.special === "cold") g.coldSnapTurns = card.duration;
    if (card.special === "drought") g.droughtActive = true;
    if (card.special === "heat") g.heatWaveActive = true;
    addLog(g, `🌧️ ${card.name}: ${card.load > 0 ? "+" : ""}${card.load} to all stacks for ${card.duration} turn(s).`);
  }

  function resolveRegulatory(g, card) {
    g.regulatoryStack.push({ ...card, turnsLeft: card.duration });
    if (card.special === "consent") {
      g.consentDecreeActive = true;
      addLog(g, "📋 Consent Decree: All penalties doubled!");
    } else if (card.special === "pfas") {
      for (let s of g.stacks) s.loadValue += 1;
      g.pfasApplied = true;
      addLog(g, "📋 PFAS Order: +1 to all stacks. No fix exists.");
    } else if (card.special === "tighten") {
      g.needsTargetSelection = { type: "tighten", card };
      addLog(g, "📋 Tighter Nutrients: Choose a stack to narrow to ±3.");
      return;
    } else if (card.special === "widen") {
      g.needsTargetSelection = { type: "widen", card };
      addLog(g, "📋 Permit Renewal: Choose a stack to widen to ±5.");
      return;
    }
  }

  function resolveEmergency(g, card) {
    if (card.special === "fire") {
      g.needsTargetSelection = { type: "fire", card };
      addLog(g, "🔥 Subsurface Fire! Choose a stack to devastate.");
      return;
    }
    if (card.special === "collapse") {
      for (let s of g.stacks) s.loadValue *= 2;
      g.collapseActive = true;
      addLog(g, "💥 Main Line Collapse: All load values doubled!");
    }
  }

  function handleTargetSelect(stackIdx) {
    setGame(prev => {
      const g = JSON.parse(JSON.stringify(prev));
      const sel = g.needsTargetSelection;
      if (!sel) return prev;

      if (sel.type === "tighten") {
        g.stacks[stackIdx].rangeMin = -3;
        g.stacks[stackIdx].rangeMax = 3;
        g.tightenedStack = stackIdx;
        addLog(g, `Stack ${String.fromCharCode(65 + stackIdx)} range narrowed to ±3.`);
      } else if (sel.type === "widen") {
        g.stacks[stackIdx].rangeMin = -5;
        g.stacks[stackIdx].rangeMax = 5;
        g.widenedStack = stackIdx;
        addLog(g, `Stack ${String.fromCharCode(65 + stackIdx)} range widened to ±5.`);
      } else if (sel.type === "fire") {
        g.stacks[stackIdx].loadValue = 6;
        g.stacks[stackIdx].fireBlock = 2;
        addLog(g, `🔥 Stack ${String.fromCharCode(65 + stackIdx)} set to +6! No ops for 2 turns.`);
      }

      g.needsTargetSelection = null;
      return g;
    });
  }

  function resolveImmediateCards(g) {
    // Check hand for immediate cards and resolve them
    let changed = true;
    while (changed) {
      changed = false;
      for (let i = g.hand.length - 1; i >= 0; i--) {
        const card = g.hand[i];
        if (IMMEDIATE_CATS.includes(card.category)) {
          g.hand.splice(i, 1);
          if (card.category === "weather") resolveWeather(g, card);
          else if (card.category === "regulatory") resolveRegulatory(g, card);
          else if (card.category === "emergency") resolveEmergency(g, card);
          changed = true;
          break; // restart loop since indices shifted
        }
      }
    }
  }

  function playCard(stackIdx) {
    if (game.phase !== "operator" || game.needsTargetSelection) return;
    if (selectedCard === null) return;

    const card = game.hand[selectedCard];
    if (!card) return;

    // Check fire block for ops cards
    if (card.category === "operational" && game.stacks[stackIdx].fireBlock > 0) {
      return; // can't play ops here
    }

    // Check overtime already used
    if (card.special === "wild" && game.overtimeUsed) {
      return;
    }

    setGame(prev => {
      const g = JSON.parse(JSON.stringify(prev));
      const playedCard = g.hand.splice(selectedCard, 1)[0];
      playCardOnStack(g, playedCard, stackIdx);
      g.cardsPlayedThisTurn++;
      return g;
    });
    setSelectedCard(null);
  }

  function endOperatorPhase() {
    setGame(prev => {
      const g = JSON.parse(JSON.stringify(prev));

      // Check hand limit — discard excess to violations
      while (g.hand.length > 5) {
        const discarded = g.hand.pop();
        g.violationsPile.push(discarded);
        addLog(g, `🗑️ ${discarded.name} sent to Violations Pile.`);
      }

      // Industrial clog check
      const indCards = g.hand.filter(c => c.category === "industrial");
      if (indCards.length >= 3) {
        // Must play on most recently played stack
        const target = g.lastPlayedStack !== null ? g.lastPlayedStack : 0;
        const idx = g.hand.findIndex(c => c.category === "industrial");
        const forced = g.hand.splice(idx, 1)[0];
        playCardOnStack(g, forced, target);
        addLog(g, `🏭 FORCED: 3 industrial cards — ${forced.name} dumped on Stack ${String.fromCharCode(65 + target)}!`);
      } else if (indCards.length >= 2) {
        // Player should play one (simplified: we let them, but flag it)
        addLog(g, "⚠️ Holding 2 industrial cards — must play one.");
        // For simplicity in prototype: auto-force one to player's choice of last stack
        // Actually let's just warn and let them handle it next turn
      }

      // Score
      let outOfBalance = 0;
      for (const s of g.stacks) {
        if (s.loadValue < s.rangeMin || s.loadValue > s.rangeMax) outOfBalance++;
      }

      let scoreDelta = 0;
      if (outOfBalance === 0) {
        g.streak++;
        scoreDelta = g.streak;
        addLog(g, `✅ All balanced! Streak: ${g.streak} → +${scoreDelta} points`);
      } else if (outOfBalance === 1) {
        g.streak = 0;
        addLog(g, `⚠️ 1 stack out of balance. +0 points. Streak reset.`);
      } else if (outOfBalance === 2) {
        g.streak = 0;
        scoreDelta = g.consentDecreeActive ? -2 : -1;
        addLog(g, `❌ 2 stacks out! ${scoreDelta} points.`);
      } else {
        g.streak = 0;
        scoreDelta = g.consentDecreeActive ? -4 : -2;
        addLog(g, `💀 ALL 3 out! ${scoreDelta} points.`);
      }
      g.score += scoreDelta;

      if (g.score <= -5) {
        g.phase = "gameover";
        addLog(g, "☠️ FACILITY SHUTDOWN. Score dropped to -5.");
        return g;
      }

      g.phase = "world";
      g.cardsPlayedThisTurn = 0;
      addLog(g, "--- World Phase: 3 cards incoming... ---");
      return g;
    });
  }

  function executeWorldPhase() {
    setGame(prev => {
      const g = JSON.parse(JSON.stringify(prev));

      for (let i = 0; i < 3; i++) {
        if (g.deck.length === 0) break;
        const card = g.deck.shift();
        if (IMMEDIATE_CATS.includes(card.category)) {
          // Immediate cards go to their side stacks
          if (card.category === "weather") resolveWeather(g, card);
          else if (card.category === "regulatory") resolveRegulatory(g, card);
          else if (card.category === "emergency") resolveEmergency(g, card);
          i--; // doesn't count as a stack placement — draw another
          continue;
        }
        playCardOnStack(g, card, i);
        addLog(g, `🎲 World dealt ${card.name} to Stack ${String.fromCharCode(65 + i)}`);
      }

      // Tick weather durations
      for (let i = g.weatherStack.length - 1; i >= 0; i--) {
        g.weatherStack[i].turnsLeft--;
        if (g.weatherStack[i].turnsLeft <= 0) {
          const expired = g.weatherStack.splice(i, 1)[0];
          // Remove global effects
          for (let s of g.stacks) s.loadValue -= expired.load;
          if (expired.special === "cold") g.coldSnapTurns = 0;
          if (expired.special === "drought") g.droughtActive = false;
          if (expired.special === "heat") g.heatWaveActive = false;
          addLog(g, `🌤️ ${expired.name} expired.`);
        }
      }

      // Tick fire blocks
      for (let s of g.stacks) {
        if (s.fireBlock > 0) s.fireBlock--;
      }

      // Collapse expires after one turn
      if (g.collapseActive) {
        for (let s of g.stacks) s.loadValue = Math.round(s.loadValue / 2);
        g.collapseActive = false;
        addLog(g, "Main Line Collapse effect expired.");
      }

      g.turn++;
      g.cardsPlayedThisTurn = 0;

      // Check if deck is empty and hand is empty
      if (g.deck.length === 0 && g.hand.length === 0) {
        g.phase = "won";
        // End of game bonuses
        let bonus = 0;
        let outOfBalance = 0;
        for (const s of g.stacks) {
          if (s.loadValue < s.rangeMin || s.loadValue > s.rangeMax) outOfBalance++;
        }
        if (outOfBalance === 0) { bonus += 3; addLog(g, "🏆 Clean final inspection: +3"); }
        bonus -= g.violationsPile.length;
        if (g.violationsPile.length > 0) addLog(g, `🗑️ Violations pile penalty: -${g.violationsPile.length}`);
        g.score += bonus;
        addLog(g, `🎉 GAME OVER. Final score: ${g.score}`);
        return g;
      }

      // Draw phase for next operator turn
      if (g.skipNextOperator) {
        g.skipNextOperator = false;
        addLog(g, "☠️ Operator phase SKIPPED (toxic recovery).");
        g.phase = "world"; // will need another world phase... simplify: just go to operator with no plays
        // Actually, let's just skip the play but still draw and score
      }

      // Draw to 5
      while (g.hand.length < 5 && g.deck.length > 0) {
        g.hand.push(g.deck.shift());
      }

      // Resolve immediate cards from draw
      resolveImmediateCards(g);

      g.phase = "operator";
      if (g.skipNextOperator) {
        addLog(g, "Turn " + g.turn + ": ☠️ Toxic recovery — you may only play 0 cards.");
      } else {
        addLog(g, "Turn " + g.turn + ": Play 1–3 cards.");
      }

      return g;
    });
  }

  function forceIndustrialPlay(stackIdx) {
    // When player has 2+ industrial and must play one
    setGame(prev => {
      const g = JSON.parse(JSON.stringify(prev));
      const idx = g.hand.findIndex(c => c.category === "industrial");
      if (idx >= 0) {
        const card = g.hand.splice(idx, 1)[0];
        playCardOnStack(g, card, stackIdx);
        addLog(g, `🏭 Played industrial card ${card.name} on Stack ${String.fromCharCode(65 + stackIdx)}.`);
      }
      return g;
    });
  }

  const canEndPhase = game.phase === "operator" && game.cardsPlayedThisTurn >= 1;
  const canPlayMore = game.phase === "operator" && game.cardsPlayedThisTurn < 3 && !game.skipNextOperator;
  const mustPlayOne = game.phase === "operator" && game.cardsPlayedThisTurn === 0 && !game.skipNextOperator;

  // Check if any stack needs target selection
  if (game.needsTargetSelection) {
    return (
      <div style={{ fontFamily: "monospace", maxWidth: 700, margin: "0 auto", padding: 16, background: "#f5f5f5", minHeight: "100vh" }}>
        <h2 style={{ textAlign: "center", fontSize: 16 }}>🎯 {game.needsTargetSelection.type === "fire" ? "Choose stack to devastate" : game.needsTargetSelection.type === "tighten" ? "Choose stack to narrow (±3)" : "Choose stack to widen (±5)"}</h2>
        <div style={{ display: "flex", gap: 8, justifyContent: "center", marginTop: 16 }}>
          {game.stacks.map((s, i) => (
            <button key={i} onClick={() => handleTargetSelect(i)} style={{ padding: "12px 24px", fontSize: 14, cursor: "pointer", background: "#1565c0", color: "#fff", border: "none", borderRadius: 6 }}>
              Stack {String.fromCharCode(65 + i)} (Load: {s.loadValue})
            </button>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div style={{ fontFamily: "monospace", maxWidth: 780, margin: "0 auto", padding: "8px 12px", background: "#f5f5f5", minHeight: "100vh" }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8, flexWrap: "wrap", gap: 4 }}>
        <div>
          <span style={{ fontSize: 18, fontWeight: "bold" }}>🚽 Sludgeitaire</span>
          <span style={{ fontSize: 10, color: "#888", marginLeft: 8 }}>Turn {game.turn}</span>
        </div>
        <div style={{ display: "flex", gap: 12, fontSize: 12, alignItems: "center" }}>
          <span>Score: <strong style={{ color: game.score >= 0 ? "#2e7d32" : "#c62828", fontSize: 16 }}>{game.score}</strong></span>
          <span>Streak: <strong style={{ color: "#e65100" }}>{game.streak}</strong></span>
          <span>Deck: <strong>{game.deck.length}</strong></span>
          {game.violationsPile.length > 0 && <span style={{ color: "#c62828" }}>Violations: {game.violationsPile.length}</span>}
        </div>
      </div>

      {/* Phase indicator */}
      <div style={{ textAlign: "center", padding: "4px 8px", marginBottom: 8, borderRadius: 4, fontSize: 11,
        background: game.phase === "operator" ? "#e8f5e9" : game.phase === "world" ? "#e3f2fd" : game.phase === "gameover" ? "#ffebee" : "#e8f5e9",
        border: `1px solid ${game.phase === "operator" ? "#4caf50" : game.phase === "world" ? "#1565c0" : "#c62828"}`
      }}>
        {game.phase === "operator" && (game.skipNextOperator
          ? "☠️ TOXIC RECOVERY — Click End Phase (no plays allowed)"
          : `🔧 OPERATOR PHASE — Play ${3 - game.cardsPlayedThisTurn} more card(s) (${game.cardsPlayedThisTurn}/3 played) ${mustPlayOne ? "• Must play at least 1" : ""}`
        )}
        {game.phase === "world" && "🌍 WORLD PHASE — Click to deal 3 random cards"}
        {game.phase === "gameover" && "☠️ FACILITY SHUTDOWN"}
        {game.phase === "won" && `🏆 ALL CARDS PLAYED — Final Score: ${game.score}`}
      </div>

      {/* Side stacks + Main stacks */}
      <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
        {/* Weather side stack */}
        <div style={{ width: 80, flexShrink: 0 }}>
          <div style={{ fontSize: 9, fontWeight: "bold", color: "#1565c0", textAlign: "center", marginBottom: 4 }}>🌧️ Weather</div>
          {game.weatherStack.length === 0 && <div style={{ fontSize: 8, color: "#999", textAlign: "center" }}>Clear</div>}
          {game.weatherStack.map((c, i) => (
            <div key={i} style={{ fontSize: 8, background: "#e3f2fd", borderRadius: 3, padding: 3, marginBottom: 2, borderLeft: "3px solid #1565c0" }}>
              <div style={{ fontWeight: "bold" }}>{c.name}</div>
              <div>{c.turnsLeft}t left | {c.load > 0 ? "+" : ""}{c.load}/stack</div>
            </div>
          ))}
          <div style={{ fontSize: 9, fontWeight: "bold", color: "#6a1b9a", textAlign: "center", marginTop: 8, marginBottom: 4 }}>📋 Regulatory</div>
          {game.regulatoryStack.length === 0 && <div style={{ fontSize: 8, color: "#999", textAlign: "center" }}>None</div>}
          {game.regulatoryStack.map((c, i) => (
            <div key={i} style={{ fontSize: 8, background: "#f3e5f5", borderRadius: 3, padding: 3, marginBottom: 2, borderLeft: "3px solid #6a1b9a" }}>
              <div style={{ fontWeight: "bold" }}>{c.name}</div>
              <div>{c.flavor}</div>
            </div>
          ))}
        </div>

        {/* Three treatment stacks */}
        <div style={{ display: "flex", gap: 6, flex: 1 }}>
          {game.stacks.map((s, i) => (
            <StackView
              key={i}
              stack={s}
              index={i}
              label={`${s.loadValue >= s.rangeMin && s.loadValue <= s.rangeMax ? "✅" : "❌"}`}
              highlight={selectedCard !== null && canPlayMore && game.phase === "operator"}
              onDrop={() => { if (selectedCard !== null && canPlayMore) playCard(i); }}
            />
          ))}
        </div>
      </div>

      {/* Active effects summary */}
      <div style={{ display: "flex", gap: 4, flexWrap: "wrap", marginBottom: 6 }}>
        {game.coldSnapTurns > 0 && <span style={{ fontSize: 8, background: "#bbdefb", padding: "2px 6px", borderRadius: 10 }}>❄️ Cold Snap: Ops -1 ({game.coldSnapTurns}t)</span>}
        {game.droughtActive && <span style={{ fontSize: 8, background: "#fff3e0", padding: "2px 6px", borderRadius: 10 }}>☀️ Drought: Industrial +2</span>}
        {game.heatWaveActive && <span style={{ fontSize: 8, background: "#ffebee", padding: "2px 6px", borderRadius: 10 }}>🔥 Heat: &gt;+3 → +5</span>}
        {game.consentDecreeActive && <span style={{ fontSize: 8, background: "#f3e5f5", padding: "2px 6px", borderRadius: 10 }}>📋 Consent Decree: 2× penalties</span>}
        {game.overtimeUsed && <span style={{ fontSize: 8, background: "#e0e0e0", padding: "2px 6px", borderRadius: 10 }}>🔧 Overtime used</span>}
      </div>

      {/* Hand */}
      <div style={{ marginBottom: 8 }}>
        <div style={{ fontSize: 10, fontWeight: "bold", marginBottom: 4 }}>
          Your Hand ({game.hand.length})
          {industrialInHand >= 2 && <span style={{ color: "#c62828", marginLeft: 8 }}>⚠️ {industrialInHand} industrial cards!</span>}
        </div>
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {game.hand.map((c, i) => (
            <Card
              key={c.id}
              card={c}
              selected={selectedCard === i}
              onClick={() => {
                if (game.phase !== "operator") return;
                if (IMMEDIATE_CATS.includes(c.category)) return; // shouldn't be in hand but safety
                setSelectedCard(selectedCard === i ? null : i);
              }}
            />
          ))}
          {game.hand.length === 0 && <div style={{ fontSize: 10, color: "#999" }}>Empty</div>}
        </div>
        {selectedCard !== null && (
          <div style={{ fontSize: 9, color: "#1565c0", marginTop: 4, display: "flex", alignItems: "center", gap: 8 }}>
            <span>Click a stack to play {game.hand[selectedCard]?.name}</span>
            <span style={{ color: "#999" }}>or</span>
            <button
              onClick={() => {
                if (selectedCard === null || game.phase !== "operator") return;
                setGame(prev => {
                  const g = JSON.parse(JSON.stringify(prev));
                  const discarded = g.hand.splice(selectedCard, 1)[0];
                  g.violationsPile.push(discarded);
                  g.cardsPlayedThisTurn++;
                  addLog(g, `🗑️ Discarded ${discarded.name} to Violations Pile. (-1 at end of game)`);
                  return g;
                });
                setSelectedCard(null);
              }}
              style={{
                padding: "3px 10px", fontSize: 9, fontFamily: "monospace", cursor: "pointer",
                background: "#c62828", color: "#fff", border: "none", borderRadius: 3,
              }}
            >
              🗑️ Discard to Violations (-1 pt)
            </button>
          </div>
        )}
      </div>

      {/* Violations Pile */}
      {game.violationsPile.length > 0 && (
        <div style={{ marginBottom: 8, padding: 6, background: "#ffebee", border: "1px solid #ef9a9a", borderRadius: 4 }}>
          <div style={{ fontSize: 10, fontWeight: "bold", color: "#c62828", marginBottom: 4 }}>
            🗑️ Violations Pile ({game.violationsPile.length} cards = -{game.violationsPile.length} pts at end)
          </div>
          <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
            {game.violationsPile.map((c, i) => (
              <div key={c.id} style={{ fontSize: 8, background: "#fff", borderRadius: 3, padding: "2px 6px", border: "1px solid #ef9a9a", display: "flex", alignItems: "center", gap: 3 }}>
                <span>{CAT_ICONS[c.category]}</span>
                <span>{c.name}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Action buttons */}
      <div style={{ display: "flex", gap: 8, marginBottom: 8 }}>
        {game.phase === "operator" && (
          <button
            onClick={endOperatorPhase}
            disabled={!canEndPhase && !game.skipNextOperator}
            style={{
              padding: "8px 16px", fontSize: 11, fontFamily: "monospace", cursor: canEndPhase || game.skipNextOperator ? "pointer" : "not-allowed",
              background: canEndPhase || game.skipNextOperator ? "#2e7d32" : "#ccc", color: "#fff", border: "none", borderRadius: 4,
            }}
          >
            {game.skipNextOperator ? "End Phase (Skipped)" : `End Operator Phase${mustPlayOne ? " (play 1 first)" : ""}`}
          </button>
        )}
        {game.phase === "world" && (
          <button onClick={executeWorldPhase} style={{ padding: "8px 16px", fontSize: 11, fontFamily: "monospace", cursor: "pointer", background: "#1565c0", color: "#fff", border: "none", borderRadius: 4 }}>
            🌍 Deal World Phase Cards
          </button>
        )}
        {(game.phase === "gameover" || game.phase === "won") && (
          <button onClick={() => { setGame(initGame()); setSelectedCard(null); }} style={{ padding: "8px 16px", fontSize: 11, fontFamily: "monospace", cursor: "pointer", background: "#e65100", color: "#fff", border: "none", borderRadius: 4 }}>
            🔄 New Game
          </button>
        )}
      </div>

      {/* Log */}
      <div style={{ background: "#263238", color: "#b0bec5", padding: 8, borderRadius: 4, fontSize: 9, maxHeight: 120, overflowY: "auto" }}>
        <div style={{ fontSize: 8, color: "#546e7a", marginBottom: 4 }}>OPERATOR LOG</div>
        {game.log.map((msg, i) => (
          <div key={i} style={{ color: msg.includes("❌") || msg.includes("☠️") || msg.includes("💀") ? "#ef9a9a" : msg.includes("✅") || msg.includes("🏆") ? "#a5d6a7" : "#b0bec5" }}>{msg}</div>
        ))}
      </div>
    </div>
  );
}
