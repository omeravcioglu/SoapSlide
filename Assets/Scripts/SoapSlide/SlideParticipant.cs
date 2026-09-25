using System.Collections.Generic;
#if NETCODE
using Ignitives.MultiplayerEngine;
using Unity.Netcode;
#endif
using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Offline: local rigidbody simulation.
    /// Online: only the server integrates physics; clients are non-authoritative replicas driven by Netcode
    /// <c>NetworkTransform</c> + <c>NetworkRigidbody</c> for smooth motion.
    /// </summary>
    public sealed class SlideParticipant : MonoBehaviour
    {
        string _displayName = "Player";

        [SerializeField] float _mass = 1f;
        [SerializeField] float _linearDamping = 0.26f;
        [SerializeField] float _angularDamping = 0.48f;
        [SerializeField, Range(0.2f, 1f)] float _botImpulseMultiplier = 0.58f;

        Rigidbody _rb;
        ISoapSlideMatchDirector _director;
        /// <summary>True only for this machine's controllable capsule (arrow, local aim).</summary>
        bool _localHuman;

#if NETCODE
        bool _networkClientReplica;
#endif
        /// <summary>True for a human-controlled roster slot (not AI filler).</summary>
        bool _realPlayerSlot;
        Color _color;

        Vector2 _planXZ = Vector2.up;
        int _planForceLevel = 5;
        float _planForce01;

        SlidePlanningArrow _planningArrow;

        int _slotIndex;
        int _teamIndex = -1;

        public bool IsHuman => _localHuman;
        public bool IsRealPlayerSlot => _realPlayerSlot;
        public bool IsEliminated { get; private set; }
        public Rigidbody Body => _rb;
        public int SlotIndex => _slotIndex;
        public int TeamIndex => _teamIndex;

        public Vector3 PlanDirectionWorld
        {
            get
            {
                var d2 = new Vector3(_planXZ.x, 0f, _planXZ.y);
                return d2.sqrMagnitude > 0.0001f ? d2.normalized : Vector3.forward;
            }
        }

        public int PlanForceLevel
        {
            get
            {
                if (_realPlayerSlot)
                    return Mathf.Clamp(_planForceLevel, 1, 10);
                return Mathf.RoundToInt(_planForce01 * 10f);
            }
        }
        public float BotForce01 => _planForce01;
        public string DisplayName => _displayName;

        public void Init(ISoapSlideMatchDirector director, bool localHuman, bool realPlayerSlot, Color color,
            PhysicsMaterial sharedMaterial,
            string displayName = null, int slotIndex = 0, int teamIndex = -1)
        {
            _director = director;
            _localHuman = localHuman;
            _realPlayerSlot = realPlayerSlot;
            _color = color;
            _slotIndex = slotIndex;
            _teamIndex = teamIndex;
            _displayName = string.IsNullOrEmpty(displayName) ? (realPlayerSlot ? "Player" : "Rival") : displayName.Trim();
            if (_displayName.Length > 28)
                _displayName = _displayName.Substring(0, 28);
            gameObject.name = _displayName;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.mass = _mass;
                _rb.useGravity = true;
                _rb.linearDamping = _linearDamping;
                _rb.angularDamping = _angularDamping;
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            else
            {
                _rb.mass = _mass;
                _rb.useGravity = true;
                _rb.linearDamping = _linearDamping;
                _rb.angularDamping = _angularDamping;
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            var col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.45f;
                col.center = Vector3.up * 0.5f;
            }

            col.material = sharedMaterial;

            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                SoapSlideVisualUtil.ApplyCompatibleLitMaterial(rend, color);
            }

            if (_localHuman && _planningArrow == null)
            {
                var arrowGo = new GameObject("PlanningArrow");
                arrowGo.transform.SetParent(transform, false);
                _planningArrow = arrowGo.AddComponent<SlidePlanningArrow>();
                _planningArrow.Init(this, color);
            }

#if NETCODE
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && _rb != null)
            {
                _networkClientReplica = true;
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
#endif
        }

        /// <summary>Used when the Netcode owner spawns: enables local aim/planning on this build only.</summary>
        public void ApplyNetworkLocalHuman(bool isLocalOwner)
        {
            _localHuman = isLocalOwner;
            if (_localHuman && _planningArrow == null && _rb != null)
            {
                var arrowGo = new GameObject("PlanningArrow");
                arrowGo.transform.SetParent(transform, false);
                _planningArrow = arrowGo.AddComponent<SlidePlanningArrow>();
                _planningArrow.Init(this, _color);
            }
        }

        /// <summary>Server applies latest plan from owning client (Planning phase).</summary>
        public void SetServerPlanXZ(float dirX, float dirZ, int forceLevel)
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            var xz = new Vector2(dirX, dirZ);
            if (xz.sqrMagnitude > 0.0001f)
                _planXZ = xz.normalized;
            _planForceLevel = Mathf.Clamp(forceLevel, 1, 10);
        }

        /// <summary>Server: tint from <see cref="GameContentLibrary"/> character entry.</summary>
        public void ApplyCharacterFromIdServer(string characterId)
        {
#if NETCODE
            if (string.IsNullOrEmpty(characterId) || characterId == "bot")
                return;
            var lib = GameContentLibrary.Instance;
            if (lib?.CharacterData == null) return;
            CharacterData data = null;
            foreach (var c in lib.CharacterData)
            {
                if (c != null && c.CharacterId == characterId)
                {
                    data = c;
                    break;
                }
            }

            var rend = GetMemberRenderer();
            if (rend == null) return;
            if (data != null)
                SoapSlideVisualUtil.ApplyCompatibleLitMaterial(rend, ColorForCharacterId(data.CharacterId));
            else
                SoapSlideVisualUtil.ApplyCompatibleLitMaterial(rend, ColorForCharacterId(characterId));
#endif
        }

#if NETCODE
        static Color ColorForCharacterId(string id)
        {
            unchecked
            {
                int h = id.GetHashCode();
                return Color.HSVToRGB((h & 0xFF) / 255f, 0.55f, 0.92f);
            }
        }

        /// <summary>Client replicas: director is <see cref="SoapSlideClientDirectorView"/>; physics is kinematic.</summary>
        public void InitReplicaFromNetwork(SoapSlidePlayerNet net)
        {
            if (net == null) return;
            _director = Object.FindFirstObjectByType<SoapSlideClientDirectorView>();
            _slotIndex = net.SlotIndex.Value;
            _teamIndex = net.TeamIndexNet.Value;
            _realPlayerSlot = !net.IsBotPlayer.Value;
            _displayName = net.DisplayName.Value.ToString();
            if (string.IsNullOrEmpty(_displayName))
                _displayName = _realPlayerSlot ? "Player" : "Rival";
            if (_displayName.Length > 28)
                _displayName = _displayName.Substring(0, 28);
            gameObject.name = _displayName;
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            _networkClientReplica = true;

            if (!net.IsBotPlayer.Value)
            {
                var cid = net.CharacterId.Value.ToString();
                if (!string.IsNullOrEmpty(cid))
                    ApplyCharacterFromIdServer(cid);
                else
                {
                    var rendHuman = GetMemberRenderer();
                    if (rendHuman != null)
                    {
                        var c = ColorForCharacterId(_displayName);
                        SoapSlideVisualUtil.ApplyCompatibleLitMaterial(rendHuman, c);
                    }
                }
            }
            else
            {
                var rend = GetMemberRenderer();
                if (rend != null)
                {
                    var c = ColorForCharacterId(net.DisplayName.Value.ToString());
                    SoapSlideVisualUtil.ApplyCompatibleLitMaterial(rend, c);
                }
            }
        }
#endif

        /// <summary>Remote clients: hide / disable without server physics.</summary>
        public void ApplyEliminatedVisualOnly()
        {
            if (IsEliminated) return;
            IsEliminated = true;
            if (_planningArrow != null)
                _planningArrow.SetVisible(false);
            if (_rb != null)
            {
                _rb.detectCollisions = false;
                _rb.isKinematic = true;
            }

            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                var c = _color;
                c.a = 0.25f;
                rend.material.color = c;
            }

            gameObject.SetActive(false);
        }

        public void Teleport(Vector3 position)
        {
            transform.position = position;
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        public void SetPlanningFrozen(bool frozen)
        {
            // Non-simulating clients: server drives the rigidbody; only mirror planning-arrow visibility from phase RPCs.
            if (SoapSlideNetUtils.IsNetcodeActive && !SoapSlideNetUtils.ShouldRunGameSimulation)
            {
                if (_planningArrow != null)
                    _planningArrow.SetVisible(frozen);
                return;
            }

            if (_rb == null) return;
            if (frozen)
            {
                // Clear velocity while still dynamic — kinematic bodies cannot have velocity assigned in Unity 6+.
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                _rb.isKinematic = true;
            }
            else
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.WakeUp();
            }

            if (_planningArrow != null)
                _planningArrow.SetVisible(frozen);
        }

        /// <param name="momentum">Change in momentum (mass·Δv).</param>
        void ApplySlideMomentum(Vector3 momentum)
        {
            if (_rb == null || IsEliminated) return;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.WakeUp();
            float invMass = 1f / Mathf.Max(_rb.mass, 0.0001f);
            Vector3 deltaV = momentum * invMass;
            Vector3 v = _rb.linearVelocity;
            v.x += deltaV.x;
            v.y += deltaV.y;
            v.z += deltaV.z;
            _rb.linearVelocity = v;
        }

        public void BeginPlanningRound(int roundSeed, IReadOnlyList<SlideParticipant> everyoneAlive)
        {
            if (!_realPlayerSlot)
                RollBotPlan(roundSeed, everyoneAlive);
        }

        public void TickPlanning(float planningSeconds)
        {
            if (IsEliminated || _director == null) return;

            if (_localHuman)
            {
                UpdateHumanPlan();
                _planningArrow?.Refresh();
            }
        }

        void Update()
        {
            if (!_localHuman || IsEliminated || _director == null) return;
            if (_director.Phase != SoapSlidePhase.Planning || !_director.MatchIsActive) return;

            _planForceLevel = SoapSlideInput.ReadForceLevel1To10(_planForceLevel);

            UpdateHumanPlan();
            _planningArrow?.Refresh();
        }

        void UpdateHumanPlan()
        {
            Camera cam = Camera.main;
            bool aimed = SoapSlideInput.TryReadMouseSlideDirection(transform, cam, out Vector2 xz);
            if (aimed)
                _planXZ = xz;
            else
            {
                Vector2 stick = SoapSlideInput.ReadMoveXZ();
                if (stick.sqrMagnitude > 0.01f)
                    _planXZ = stick.normalized;
            }
        }

        bool IsBotOpponent(SlideParticipant p)
        {
            if (p == null || p == this) return false;
            if (_teamIndex < 0 || p.TeamIndex < 0) return true;
            return p.TeamIndex != _teamIndex;
        }

        void RollBotPlan(int roundSeed, IReadOnlyList<SlideParticipant> everyone)
        {
            int seed = GetInstanceID() ^ roundSeed;
            var rng = new System.Random(seed);

            float arena = _director != null ? _director.ArenaHalfExtents : 12f;
            Vector2 myXZ = new Vector2(transform.position.x, transform.position.z);
            float myDist = myXZ.magnitude;

            var opponents = new List<SlideParticipant>(8);
            if (everyone != null)
            {
                foreach (var p in everyone)
                {
                    if (p != null && p != this && !p.IsEliminated && IsBotOpponent(p))
                        opponents.Add(p);
                }
            }

            if (opponents.Count == 0)
            {
                Vector2 towardCenter = myXZ.sqrMagnitude > 0.01f ? -myXZ.normalized : Vector2.up;
                float jitter = (float)(rng.NextDouble() * Mathf.PI * 2d) * 0.35f;
                Vector2 j = Rotate(towardCenter, jitter);
                _planXZ = Vector2.Lerp(j, towardCenter, 0.65f).normalized;
                _planForce01 = Mathf.Clamp01(0.22f + (float)rng.NextDouble() * 0.2f);
                return;
            }

            SlideParticipant target = PickBotTarget(opponents, rng, arena, myXZ);
            SlideParticipant nearest = PickNearestOpponent(opponents, myXZ);

            Vector2 tXZ = new Vector2(target.transform.position.x, target.transform.position.z);

            Vector2 toTarget = tXZ - myXZ;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = RandomInsideUnitCircle(rng);
            toTarget.Normalize();

            // Knockout aim: intercept + push along rim direction through target (sends them toward the edge).
            Vector2 radialThroughTarget = tXZ.sqrMagnitude > 0.0001f ? tXZ.normalized : toTarget;
            float knockoutBlend = 0.48f + (float)rng.NextDouble() * 0.32f;
            Vector2 dir = (toTarget * (1f - knockoutBlend) + radialThroughTarget * knockoutBlend).normalized;

            // Dodge: if someone is very close, sidestep (perpendicular) while keeping rough intent toward target.
            if (nearest != null)
            {
                Vector2 nXZ = new Vector2(nearest.transform.position.x, nearest.transform.position.z);
                Vector2 sep = myXZ - nXZ;
                float close = sep.magnitude;
                float dodgeRadius = arena * (0.28f + (float)rng.NextDouble() * 0.08f);
                if (close < dodgeRadius && close > 0.0001f)
                {
                    Vector2 away = sep / close;
                    Vector2 perp = new Vector2(-away.y, away.x);
                    if (Vector2.Dot(perp, dir) < 0f)
                        perp = -perp;
                    float dodgeW = 0.24f + (float)rng.NextDouble() * 0.22f;
                    if (close < dodgeRadius * 0.55f)
                        dodgeW += 0.12f;
                    dir = (dir * (1f - dodgeW) + perp * dodgeW).normalized;
                }
            }

            float edgeStart = arena * 0.72f;
            if (myDist > edgeStart)
            {
                float t = Mathf.Clamp01((myDist - edgeStart) / Mathf.Max(arena - edgeStart, 0.1f));
                Vector2 towardCenter = -myXZ.normalized;
                dir = Vector2.Lerp(dir, towardCenter, t * 0.78f).normalized;
            }

            if (myDist > arena * 0.92f)
                dir = Vector2.Lerp(dir, -myXZ.normalized, 0.94f).normalized;

            _planXZ = dir;

            float targetRadial = tXZ.magnitude;
            float baseForce = 0.34f + (float)rng.NextDouble() * 0.24f;
            if (targetRadial > arena * 0.52f)
                baseForce += 0.07f + (float)rng.NextDouble() * 0.1f;
            if (nearest != null)
            {
                Vector2 nXZ = new Vector2(nearest.transform.position.x, nearest.transform.position.z);
                if (Vector2.Distance(myXZ, nXZ) < arena * 0.34f)
                    baseForce += 0.05f;
            }

            if (myDist > edgeStart)
                baseForce *= Mathf.Lerp(1f, 0.52f, Mathf.Clamp01((myDist - edgeStart) / (arena * 0.35f)));

            _planForce01 = Mathf.Clamp01(baseForce);
        }

        static SlideParticipant PickNearestOpponent(List<SlideParticipant> opponents, Vector2 myXZ)
        {
            SlideParticipant best = null;
            float bestD = float.MaxValue;
            foreach (var o in opponents)
            {
                if (o == null) continue;
                Vector2 oxz = new Vector2(o.transform.position.x, o.transform.position.z);
                float d = Vector2.Distance(myXZ, oxz);
                if (d < bestD)
                {
                    bestD = d;
                    best = o;
                }
            }

            return best;
        }

        static SlideParticipant PickBotTarget(List<SlideParticipant> opponents, System.Random rng, float arena,
            Vector2 myXZ)
        {
            if (opponents.Count == 1)
                return opponents[0];

            SlideParticipant best = null;
            float bestScore = -1f;
            SlideParticipant second = null;
            float secondScore = -1f;

            foreach (var o in opponents)
            {
                Vector2 oxz = new Vector2(o.transform.position.x, o.transform.position.z);
                float dist = Vector2.Distance(myXZ, oxz);
                float edge = oxz.magnitude / Mathf.Max(arena, 0.1f);
                float proximity = 1f / (1f + dist * 0.1f);
                float score = Mathf.Clamp01(edge) * 0.55f + proximity * 0.35f;
                if (o.IsRealPlayerSlot)
                    score += 0.1f;

                if (score > bestScore)
                {
                    second = best;
                    secondScore = bestScore;
                    best = o;
                    bestScore = score;
                }
                else if (score > secondScore)
                {
                    second = o;
                    secondScore = score;
                }
            }

            if (second != null && rng.NextDouble() < 0.38f)
                return second;
            return best;
        }

        static Vector2 RandomInsideUnitCircle(System.Random rng)
        {
            double u = rng.NextDouble();
            double v = rng.NextDouble();
            float r = Mathf.Sqrt((float)u);
            float th = (float)(v * Mathf.PI * 2d);
            return new Vector2(r * Mathf.Cos(th), r * Mathf.Sin(th));
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public void ApplyCommittedImpulse(float maxImpulse)
        {
            if (_rb == null || IsEliminated) return;
            var dir = PlanDirectionWorld;
            float scale = _realPlayerSlot ? (_planForceLevel / 10f) : (_planForce01 * _botImpulseMultiplier);
            ApplySlideMomentum(dir * (maxImpulse * scale));
        }

        public SoapSlideSlotPlan BuildSlotPlanForCommit()
        {
            var d = PlanDirectionWorld;
            if (_realPlayerSlot)
            {
                return new SoapSlideSlotPlan
                {
                    DirX = d.x,
                    DirZ = d.z,
                    ForceLevel = Mathf.Clamp(_planForceLevel, 1, 10),
                    IsBot = false
                };
            }

            int f = Mathf.Clamp(Mathf.RoundToInt(_planForce01 * 10f), 1, 10);
            return new SoapSlideSlotPlan
            {
                DirX = d.x,
                DirZ = d.z,
                ForceLevel = f,
                IsBot = true
            };
        }

        public void ApplyImpulseFromSlotPlan(SoapSlideSlotPlan plan, float maxImpulse)
        {
            if (_rb == null || IsEliminated) return;
            var dir = new Vector3(plan.DirX, 0f, plan.DirZ);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            else
                dir.Normalize();

            Vector3 impulse;
            if (plan.IsBot)
            {
                float f01 = plan.ForceLevel / 10f;
                float scale = f01 * _botImpulseMultiplier;
                impulse = dir * (maxImpulse * scale);
            }
            else
            {
                float scale = Mathf.Clamp(plan.ForceLevel, 1, 10) / 10f;
                impulse = dir * (maxImpulse * scale);
            }

            ApplySlideMomentum(impulse);
        }

        public void MarkEliminated()
        {
            if (IsEliminated) return;
            IsEliminated = true;
            if (_planningArrow != null)
                _planningArrow.SetVisible(false);
            if (_rb != null)
            {
                _rb.detectCollisions = false;
                _rb.isKinematic = true;
            }

            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                var c = _color;
                c.a = 0.25f;
                rend.material.color = c;
            }

            gameObject.SetActive(false);
        }

        public void ResetForNewMatch()
        {
            IsEliminated = false;
            gameObject.SetActive(true);
            if (_rb != null)
            {
                _rb.detectCollisions = true;
                _rb.isKinematic = false;
            }
            var rend = GetMemberRenderer();
            if (rend != null)
                rend.material.color = _color;
        }

        Renderer GetMemberRenderer()
        {
            return GetComponent<Renderer>();
        }
    }
}



