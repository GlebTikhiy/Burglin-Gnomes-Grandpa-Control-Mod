using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BurglinGnomesGrandpaMod
{
    [BepInPlugin("com.yourname.grandpamod", "Grandpa Control Mod", "1.6.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(GrandpaModPatches));
            Logger.LogInfo("Grandpa Mod v1.6.0 loaded");
        }
    }

    internal static class GrandpaModPatches
    {
        private const string GrandpaChosenMessage = "GrandpaChosenMessage";
        private const string GrandpaActionMessage = "GrandpaActionMsg";

        private const byte ActionGrab = 0;
        private const byte ActionRelease = 1;
        private const byte ActionShoot = 2;
        private const byte ActionThrow = 3;

        private static bool handlersRegistered;
        private static bool serverHooksRegistered;
        private static ulong chosenGrandpaClientId = ulong.MaxValue;
        private static bool applyStateRetryRunning;

        [HarmonyPatch(typeof(GameProgressionManager), "OnNetworkSpawn")]
        [HarmonyPostfix]
        private static void OnNetworkSpawnPostfix(GameProgressionManager __instance)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
            {
                return;
            }

            var msgManager = NetworkManager.Singleton.CustomMessagingManager;
            if (!handlersRegistered)
            {
                msgManager.RegisterNamedMessageHandler(GrandpaChosenMessage, OnGrandpaChosenReceived);
                if (NetworkManager.Singleton.IsServer)
                {
                    msgManager.RegisterNamedMessageHandler(GrandpaActionMessage, OnGrandpaActionReceived);
                }

                handlersRegistered = true;
            }

            if (NetworkManager.Singleton.IsServer && !serverHooksRegistered)
            {
                __instance.onGameStarted += OnGameStarted;
                __instance.onGameReset += OnGameReset;
                serverHooksRegistered = true;
            }
        }

        [HarmonyPatch(typeof(GameProgressionManager), "OnNetworkDespawn")]
        [HarmonyPostfix]
        private static void OnNetworkDespawnPostfix(GameProgressionManager __instance)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && handlersRegistered)
            {
                var msgManager = NetworkManager.Singleton.CustomMessagingManager;
                msgManager.UnregisterNamedMessageHandler(GrandpaChosenMessage);
                if (NetworkManager.Singleton.IsServer)
                {
                    msgManager.UnregisterNamedMessageHandler(GrandpaActionMessage);
                }
            }

            if (serverHooksRegistered)
            {
                __instance.onGameStarted -= OnGameStarted;
                __instance.onGameReset -= OnGameReset;
            }

            handlersRegistered = false;
            serverHooksRegistered = false;
            chosenGrandpaClientId = ulong.MaxValue;
        }


        [HarmonyPatch(typeof(AiMovementTaskBase), "OnPathComplete")]
        [HarmonyFinalizer]
        private static Exception OnPathCompleteFinalizer(Exception __exception)
        {
            if (__exception is InvalidOperationException && __exception.Message != null && __exception.Message.Contains("Entity does not exist"))
            {
                return null;
            }

            return __exception;
        }
        private static void OnGameStarted()
        {
            var manager = GameProgressionManager.Instance;
            if (manager != null)
            {
                manager.StartCoroutine(AssignGrandpaRoutineDelayed());
            }
        }

        private static IEnumerator AssignGrandpaRoutineDelayed()
        {
            yield return new WaitForSeconds(4f);
            yield return AssignGrandpaRoutine();
        }
        private static void OnGameReset()
        {
            chosenGrandpaClientId = ulong.MaxValue;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                using (var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp))
                {
                    writer.WriteValueSafe(ulong.MaxValue);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(GrandpaChosenMessage, writer);
                }
            }

            ApplyLocalControlState(ulong.MaxValue);
        }

        private static IEnumerator AssignGrandpaRoutine()
        {
            HumanAILink grandpaAI = null;
            float timeout = 20f;
            float elapsed = 0f;

            while (grandpaAI == null && elapsed < timeout)
            {
                grandpaAI = UnityEngine.Object.FindAnyObjectByType<HumanAILink>();
                if (grandpaAI == null)
                {
                    yield return new WaitForSeconds(0.25f);
                    elapsed += 0.25f;
                }
            }

            if (grandpaAI == null)
            {
                Debug.LogWarning("[GrandpaMod] Grandpa AI not found, assignment skipped.");
                yield break;
            }

            var players = GameProgressionManager.AllPlayers;
            if (players == null || players.Count == 0)
            {
                yield break;
            }

            ulong selectedClientId = NetworkManager.ServerClientId;
            bool hostFound = false;
            foreach (var p in players)
            {
                if (p != null && p.OwnerClientId == selectedClientId)
                {
                    hostFound = true;
                    break;
                }
            }

            if (!hostFound)
            {
                selectedClientId = players[0].OwnerClientId;
                Debug.LogWarning($"[GrandpaMod] Host player not found, fallback owner {selectedClientId}.");
            }

            chosenGrandpaClientId = selectedClientId;

            var netObj = grandpaAI.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.ChangeOwnership(selectedClientId);
            }

            using (var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp))
            {
                writer.WriteValueSafe(selectedClientId);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(GrandpaChosenMessage, writer);
            }

            ApplyLocalControlState(selectedClientId);
        }

        private static void OnGrandpaChosenReceived(ulong senderClientId, FastBufferReader messagePayload)
        {
            if (!messagePayload.TryBeginRead(sizeof(ulong)))
            {
                Debug.LogWarning("[GrandpaMod] Invalid GrandpaChosen payload.");
                return;
            }

            messagePayload.ReadValueSafe(out ulong selectedClientId);
            chosenGrandpaClientId = selectedClientId;
            ApplyLocalControlState(selectedClientId);
        }

        private static void OnGrandpaActionReceived(ulong senderClientId, FastBufferReader messagePayload)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (senderClientId != chosenGrandpaClientId)
            {
                Debug.LogWarning($"[GrandpaMod] Rejected action from {senderClientId}, expected {chosenGrandpaClientId}.");
                return;
            }

            if (!messagePayload.TryBeginRead(sizeof(byte) + sizeof(ulong)))
            {
                Debug.LogWarning("[GrandpaMod] Invalid GrandpaAction payload.");
                return;
            }

            messagePayload.ReadValueSafe(out byte actionId);
            messagePayload.ReadValueSafe(out ulong targetClientId);
            ExecuteGrandpaAction(actionId, targetClientId);
        }

        internal static void ExecuteGrandpaAction(byte actionId, ulong targetClientId)
        {
            HumanAILink grandpa = UnityEngine.Object.FindAnyObjectByType<HumanAILink>();
            if (grandpa == null)
            {
                return;
            }

            if (actionId == ActionGrab)
            {
                if (grandpa.kidnapper == null)
                {
                    return;
                }

                var target = FindNearestTargetForGrandpa(grandpa);
                if (target == null)
                {
                    target = FindPlayerByClientId(targetClientId);
                }

                if (target == null)
                {
                    return;
                }

                grandpa.canPickupPlayers = true;
                grandpa.TargetedEntityInVision = target;
                grandpa.OnPlayerHitHandTrigger(target);
                TrySendCaughtEvent(grandpa);

                if (grandpa.kidnapper.CurrentlyHeld == null)
                {
                    grandpa.kidnapper.CurrentlyHeld = target;
                }

                if (grandpa.NAnimator != null)
                {
                    grandpa.NAnimator.SetTrigger("Grab");
                    grandpa.NAnimator.Animator.SetBool("Carrying", grandpa.kidnapper.CurrentlyHeld != null);
                }
            }
            else if (actionId == ActionRelease)
            {
                grandpa.ReleasePlayer();
                if (grandpa.NAnimator != null)
                {
                    grandpa.NAnimator.Animator.SetBool("Carrying", false);
                }
            }
            else if (actionId == ActionShoot)
            {
                if (grandpa.HasGun)
                {
                    grandpa.ShootUnAimed();
                }
            }
            else if (actionId == ActionThrow)
            {
                if (grandpa.kidnapper != null && grandpa.kidnapper.CurrentlyHeld != null)
                {
                    grandpa.kidnapper.RemoveHeldWithVelocity(new Vector3(0f, 1.4f, 8.5f));
                    if (grandpa.NAnimator != null)
                    {
                        grandpa.NAnimator.SetTrigger("Throw");
                        grandpa.NAnimator.Animator.SetBool("Carrying", false);
                    }
                }
            }
        }

        internal static void TrySendCaughtEvent(HumanAILink grandpa)
        {
            try
            {
                var fsmField = typeof(GameEntityAI).GetField("fsm", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var fsmObj = fsmField?.GetValue(grandpa);
                if (fsmObj == null)
                {
                    return;
                }

                var sendEvent = fsmObj.GetType().GetMethod("SendEvent", new[] { typeof(string) });
                if (sendEvent != null)
                {
                    sendEvent.Invoke(fsmObj, new object[] { HumanAILink.PlayerCaughtEventName });
                }
            }
            catch
            {
            }
        }
        private static PlayerNetworking FindNearestTargetForGrandpa(HumanAILink grandpa)
        {
            var players = GameProgressionManager.AllPlayers;
            if (players == null)
            {
                return null;
            }

            PlayerNetworking nearest = null;
            float best = float.MaxValue;
            foreach (var p in players)
            {
                if (p == null || p.OwnerClientId == chosenGrandpaClientId)
                {
                    continue;
                }

                float d = Vector3.Distance(grandpa.transform.position, p.transform.position);
                if (d < 4.2f && d < best)
                {
                    best = d;
                    nearest = p;
                }
            }

            return nearest;
        }

        private static PlayerNetworking FindPlayerByClientId(ulong clientId)
        {
            var players = GameProgressionManager.AllPlayers;
            if (players == null)
            {
                return null;
            }

            foreach (var p in players)
            {
                if (p != null && p.OwnerClientId == clientId)
                {
                    return p;
                }
            }

            return null;
        }

        private static void ApplyLocalControlState(ulong selectedClientId)
        {
            var localPlayer = ServerManager.GetLocalPlayer();
            var grandpaAI = UnityEngine.Object.FindAnyObjectByType<HumanAILink>();
            bool manualMode = selectedClientId != ulong.MaxValue;

            if (grandpaAI == null || (manualMode && localPlayer == null))
            {
                var manager = GameProgressionManager.Instance;
                if (manager != null && !applyStateRetryRunning)
                {
                    manager.StartCoroutine(ApplyLocalControlStateRetry(selectedClientId));
                }
                return;
            }

            var controller = grandpaAI.GetComponent<CustomGrandpaController>();
            if (controller == null)
            {
                controller = grandpaAI.gameObject.AddComponent<CustomGrandpaController>();
            }

            bool shouldControl = localPlayer != null && localPlayer.OwnerClientId == selectedClientId;
            controller.SetControlState(localPlayer, shouldControl, manualMode);
        }

        private static IEnumerator ApplyLocalControlStateRetry(ulong selectedClientId)
        {
            applyStateRetryRunning = true;

            for (int i = 0; i < 80; i++)
            {
                var localPlayer = ServerManager.GetLocalPlayer();
                var grandpaAI = UnityEngine.Object.FindAnyObjectByType<HumanAILink>();
                bool manualMode = selectedClientId != ulong.MaxValue;

                if (grandpaAI != null && (!manualMode || localPlayer != null))
                {
                    applyStateRetryRunning = false;
                    ApplyLocalControlState(selectedClientId);
                    yield break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            applyStateRetryRunning = false;
        }
    }

    public class CustomGrandpaController : MonoBehaviour
    {
        private const byte ActionGrab = 0;
        private const byte ActionRelease = 1;
        private const byte ActionShoot = 2;
        private const byte ActionThrow = 3;

        private const float WalkSpeed = 8.8f;
        private const float RunSpeed = 12.8f;
        private const float GrabRange = 6.0f;
        private const float GrabInterval = 0.1f;
        private const float CrouchSpeedMultiplier = 0.45f;
        private const float ExtendedHandForward = 3.2f;
        private const float ExtendedHandRadius = 1.35f;
        private const float CollisionRadius = 0.82f;
        private const float CapsuleBottom = 0.18f;
        private const float CapsuleTop = 1.55f;
        private const float CollisionSkin = 0.26f;
        private const float StepHeight = 0.42f;
        private const float AutoInteractInterval = 0.15f;
        private const float AutoInteractDistance = 1.35f;
        private const float CameraCollisionRadius = 0.22f;
        private const float DynamicPushForce = 2.4f;

        public PlayerNetworking localGnome;
        public bool isControlledByMe;

        private Animator anim;
        private Transform headBone;
        private Camera myCamera;
        private HumanAILink aiLink;

        private bool initialized;
        private float cameraPitch;
        private Vector3 controlledPosition;
        private bool grabHeld;
        private float nextGrabTime;
        private bool isCrouching;
        private bool aiSuppressed;
        private float lastMoveInputMagnitude;
        private float nextAutoInteractTime;
        private Coroutine standFixRoutine;

        private readonly List<MonoBehaviour> disabledGrandpaScripts = new List<MonoBehaviour>();
        private readonly List<MonoBehaviour> disabledCameraScripts = new List<MonoBehaviour>();
        private readonly List<BodyState> modifiedGrandpaBodies = new List<BodyState>();

        private readonly List<Renderer> hiddenGnomeRenderers = new List<Renderer>();
        private readonly List<Collider> disabledGnomeColliders = new List<Collider>();
        private readonly List<MonoBehaviour> disabledGnomeScripts = new List<MonoBehaviour>();
        private readonly List<BodyState> modifiedGnomeBodies = new List<BodyState>();

        private Vector3 ghostAnchor = new Vector3(0f, 1200f, 0f);

        private struct BodyState
        {
            public Rigidbody Rb;
            public bool WasKinematic;
            public bool HadGravity;
            public bool HadDetectCollisions;
            public RigidbodyConstraints Constraints;
        }

        private void Start()
        {
            anim = GetComponentInChildren<Animator>();
            aiLink = GetComponent<HumanAILink>();
            if (anim != null && anim.isHuman)
            {
                headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        public void SetControlState(PlayerNetworking localPlayer, bool shouldControl, bool manualMode)
        {
            localGnome = localPlayer;

            if (manualMode)
            {
                EnsureManualSuppression();
            }
            else
            {
                ReleaseManualSuppression();
            }

            if (shouldControl == isControlledByMe)
            {
                return;
            }

            isControlledByMe = shouldControl;
            if (shouldControl)
            {
                InitializeControl();
            }
            else
            {
                TeardownControl();
            }
        }

        private void EnsureManualSuppression()
        {
            if (aiSuppressed)
            {
                return;
            }

            aiSuppressed = true;
            if (aiLink != null)
            {
                aiLink.enabled = false;
            }
            DisableGrandpaLogic();
        }

        private void ReleaseManualSuppression()
        {
            if (!aiSuppressed)
            {
                return;
            }

            aiSuppressed = false;
            RestoreGrandpaLogic();
        }

        private void InitializeControl()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            controlledPosition = transform.position;
            grabHeld = false;
            nextGrabTime = 0f;


            SetupCamera();
            SetupHiddenGnome(true);
            SnapToGroundHard();
            StartForceStandRoutine();

            Debug.Log("[GrandpaMod] Local player now controls Grandpa.");
        }

        private void TeardownControl()
        {
            if (!initialized)
            {
                return;
            }

            initialized = false;
            StopForceStandRoutine();
            SetupHiddenGnome(false);
            RestoreCameraScripts();
        }


        private void StartForceStandRoutine()
        {
            StopForceStandRoutine();
            standFixRoutine = StartCoroutine(ForceStandRoutine());
        }

        private void StopForceStandRoutine()
        {
            if (standFixRoutine != null)
            {
                StopCoroutine(standFixRoutine);
                standFixRoutine = null;
            }
        }

        private IEnumerator ForceStandRoutine()
        {
            for (int i = 0; i < 35; i++)
            {
                ForceGrandpaStandUp();
                if (i < 10)
                {
                    SnapToGroundHard();
                }
                yield return new WaitForSeconds(0.1f);
            }

            standFixRoutine = null;
        }

        private void ForceGrandpaStandUp()
        {
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            if (anim != null)
            {
                TrySetAnimatorBool("Sleeping", false);
                TrySetAnimatorBool("IsSleeping", false);
                TrySetAnimatorBool("InBed", false);
                TrySetAnimatorBool("IsInBed", false);
                TrySetAnimatorBool("Lying", false);
                TrySetAnimatorBool("IsLying", false);
                TrySetAnimatorBool("Lay", false);

                if (HasAnimatorParam("WakeUp", AnimatorControllerParameterType.Trigger))
                {
                    anim.SetTrigger("WakeUp");
                }
                if (HasAnimatorParam("GetUp", AnimatorControllerParameterType.Trigger))
                {
                    anim.SetTrigger("GetUp");
                }
                if (HasAnimatorParam("StandUp", AnimatorControllerParameterType.Trigger))
                {
                    anim.SetTrigger("StandUp");
                }
            }

            if (aiLink != null)
            {
                TryInvokeNoArg(aiLink, "WakeUp");
                TryInvokeNoArg(aiLink, "GetUp");
                TryInvokeNoArg(aiLink, "StandUp");
                TryInvokeNoArg(aiLink, "ExitBed");
                TryInvokeNoArg(aiLink, "LeaveBed");
            }
        }

        private static void TryInvokeNoArg(object target, string methodName)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                var m = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                m?.Invoke(target, null);
            }
            catch
            {
            }
        }
        private void DisableGrandpaLogic()
        {
            foreach (var comp in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null || comp == this || !comp.enabled)
                {
                    continue;
                }

                string n = comp.GetType().Name.ToLowerInvariant();
                if (n.Contains("ai") || n.Contains("behaviourtree") || n.Contains("follower") || n.Contains("ik") || n.Contains("movementtask") || n.Contains("path") || n.Contains("nav") || n.Contains("fsm") || n.Contains("graphowner"))
                {
                    disabledGrandpaScripts.Add(comp);
                    comp.enabled = false;
                }
            }

            foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null)
                {
                    continue;
                }

                modifiedGrandpaBodies.Add(new BodyState
                {
                    Rb = rb,
                    WasKinematic = rb.isKinematic,
                    HadGravity = rb.useGravity,
                    HadDetectCollisions = rb.detectCollisions,
                    Constraints = rb.constraints
                });

                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = true;
            }
        }

        private void RestoreGrandpaLogic()
        {
            foreach (var comp in disabledGrandpaScripts)
            {
                if (comp != null)
                {
                    comp.enabled = true;
                }
            }
            disabledGrandpaScripts.Clear();

            foreach (var state in modifiedGrandpaBodies)
            {
                if (state.Rb != null)
                {
                    state.Rb.isKinematic = state.WasKinematic;
                    state.Rb.useGravity = state.HadGravity;
                    state.Rb.detectCollisions = state.HadDetectCollisions;
                    state.Rb.constraints = state.Constraints;
                }
            }
            modifiedGrandpaBodies.Clear();

            if (aiLink != null)
            {
                aiLink.enabled = true;
            }
        }

        private void SetupCamera()
        {
            myCamera = Camera.main;
            if (myCamera == null)
            {
                myCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }

            if (myCamera == null)
            {
                return;
            }

            foreach (var script in myCamera.GetComponents<MonoBehaviour>())
            {
                if (script == null || !script.enabled)
                {
                    continue;
                }

                disabledCameraScripts.Add(script);
                script.enabled = false;
            }

            myCamera.transform.SetParent(null);
            myCamera.fieldOfView = 75f;
        }

        private void SetupHiddenGnome(bool hide)
        {
            if (localGnome == null)
            {
                return;
            }

            if (hide)
            {
                ghostAnchor = new Vector3(0f, 1200f, 0f);
                localGnome.transform.position = ghostAnchor;
                localGnome.ToggleRagdollGravity(false);

                foreach (var renderer in localGnome.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer != null && renderer.enabled)
                    {
                        hiddenGnomeRenderers.Add(renderer);
                        renderer.enabled = false;
                    }
                }

                foreach (var col in localGnome.GetComponentsInChildren<Collider>(true))
                {
                    if (col != null && col.enabled)
                    {
                        disabledGnomeColliders.Add(col);
                        col.enabled = false;
                    }
                }

                foreach (var rb in localGnome.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (rb == null)
                    {
                        continue;
                    }

                    modifiedGnomeBodies.Add(new BodyState
                    {
                        Rb = rb,
                        WasKinematic = rb.isKinematic,
                        HadGravity = rb.useGravity,
                        HadDetectCollisions = rb.detectCollisions,
                        Constraints = rb.constraints
                    });

                    rb.isKinematic = false;
                    rb.useGravity = false;
                    rb.detectCollisions = false;
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }

                DisableGnomeInputScripts();
                return;
            }

            foreach (var renderer in hiddenGnomeRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            hiddenGnomeRenderers.Clear();

            foreach (var col in disabledGnomeColliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
            disabledGnomeColliders.Clear();

            foreach (var state in modifiedGnomeBodies)
            {
                if (state.Rb != null)
                {
                    state.Rb.isKinematic = state.WasKinematic;
                    state.Rb.useGravity = state.HadGravity;
                    state.Rb.detectCollisions = state.HadDetectCollisions;
                    state.Rb.constraints = state.Constraints;
                }
            }
            modifiedGnomeBodies.Clear();

            localGnome.ToggleRagdollGravity(true);
            RestoreGnomeInputScripts();
        }

        private void DisableGnomeInputScripts()
        {
            if (localGnome == null)
            {
                return;
            }

            foreach (var comp in localGnome.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null || !comp.enabled)
                {
                    continue;
                }

                string n = comp.GetType().Name;
                if (n.Contains("PlayerController") || n.Contains("CharacterBrain") || n.Contains("HandController") || n.Contains("CharacterStateController") || n.Contains("PhysicsActor") || n.Contains("CharacterActor"))
                {
                    if (!disabledGnomeScripts.Contains(comp))
                    {
                        disabledGnomeScripts.Add(comp);
                    }
                    comp.enabled = false;
                }
            }
        }

        private void RestoreGnomeInputScripts()
        {
            foreach (var script in disabledGnomeScripts)
            {
                if (script != null)
                {
                    script.enabled = true;
                }
            }
            disabledGnomeScripts.Clear();
        }

        private void RestoreCameraScripts()
        {
            foreach (var script in disabledCameraScripts)
            {
                if (script != null)
                {
                    script.enabled = true;
                }
            }
            disabledCameraScripts.Clear();
        }

        private void Update()
        {
            if (!isControlledByMe || myCamera == null)
            {
                return;
            }

            MaintainGhostAnchor();

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            isCrouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            UpdateCrouchAnimation();

            HandleLook(mouseX, mouseY);
            HandleMovement(mouseX);
            HandleCamera();

            bool holdingGrab = Input.GetMouseButton(0);
            if (holdingGrab && !grabHeld)
            {
                PlayLocalGrabAnimation();
                nextGrabTime = 0f;
            }
            grabHeld = holdingGrab;

            if (holdingGrab && Time.time >= nextGrabTime)
            {
                PlayLocalGrabAnimation();
                TryGrabPlayer();
                nextGrabTime = Time.time + GrabInterval;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                SendAction(ActionRelease, 0);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                SendAction(ActionThrow, 0);
            }

            if (Input.GetMouseButtonDown(1))
            {
                SendAction(ActionShoot, 0);
            }

            bool wantsInteract = Input.GetKeyDown(KeyCode.F);
            if (wantsInteract || (lastMoveInputMagnitude > 0.05f && Time.time >= nextAutoInteractTime))
            {
                TryForwardInteraction();
                nextAutoInteractTime = Time.time + AutoInteractInterval;
            }
        }

        private void PlayLocalGrabAnimation()
        {
            if (anim == null)
            {
                return;
            }

            anim.SetTrigger("Grab");
            if (aiLink != null)
            {
                GrandpaModPatches.TrySendCaughtEvent(aiLink);
            }
        }

        private void MaintainGhostAnchor()
        {
            if (localGnome == null)
            {
                return;
            }

            localGnome.transform.position = ghostAnchor;

            foreach (var state in modifiedGnomeBodies)
            {
                if (state.Rb == null)
                {
                    continue;
                }

                state.Rb.isKinematic = false;
                state.Rb.useGravity = false;
                state.Rb.detectCollisions = false;
                state.Rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            DisableGnomeInputScripts();
        }

        private void HandleLook(float mouseX, float mouseY)
        {
            transform.Rotate(Vector3.up * mouseX * 2.5f);

            cameraPitch -= mouseY * 2.5f;
            cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        }

        private void HandleMovement(float mouseX)
        {
            Vector3 move = Vector3.zero;
            float inputX = 0f;
            float inputY = 0f;

            if (Input.GetKey(KeyCode.W)) { move += transform.forward; inputY += 1f; }
            if (Input.GetKey(KeyCode.S)) { move -= transform.forward; inputY -= 1f; }
            if (Input.GetKey(KeyCode.A)) { move -= transform.right; inputX -= 1f; }
            if (Input.GetKey(KeyCode.D)) { move += transform.right; inputX += 1f; }

            lastMoveInputMagnitude = Mathf.Clamp01(new Vector2(inputX, inputY).magnitude);
            float speed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : WalkSpeed;
            if (isCrouching)
            {
                speed *= CrouchSpeedMultiplier;
            }

            Vector3 planarDelta = move.sqrMagnitude > 0f ? move.normalized * speed * Time.deltaTime : Vector3.zero;
            Vector3 newPos = ResolveCollision(controlledPosition, planarDelta);
            newPos = SnapPositionToGround(newPos);

            controlledPosition = newPos;
            transform.position = controlledPosition;

            if (anim != null)
            {
                float inMag = Mathf.Clamp01(new Vector2(inputX, inputY).magnitude);
                anim.SetFloat("InputMagnitude", inMag);
                anim.SetFloat("X", inputX);
                anim.SetFloat("Y", inputY);
                anim.SetFloat("RotationMagnitude", Mathf.Clamp01(Mathf.Abs(mouseX) * 0.35f));
                anim.SetFloat("Speed", inMag);

                if (aiLink != null && aiLink.kidnapper != null)
                {
                    anim.SetBool("Carrying", aiLink.kidnapper.CurrentlyHeld != null);
                }
            }
        }

        private Vector3 ResolveCollision(Vector3 start, Vector3 delta)
        {
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return start;
            }

            if (TryCapsuleMove(start, delta, out Vector3 moved))
            {
                return moved;
            }

            Vector3 steppedStart = start + Vector3.up * StepHeight;
            if (TryCapsuleMove(steppedStart, delta, out Vector3 stepped))
            {
                return stepped;
            }

            return moved;
        }

        private Vector3 SnapPositionToGround(Vector3 pos)
        {
            float currentY = controlledPosition.y;
            float bestDistance = float.MaxValue;
            float? groundY = null;
            Vector3 origin = pos + Vector3.up * 1.8f;

            var hits = Physics.RaycastAll(origin, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                }
            }

            if (groundY.HasValue)
            {
                float targetY = groundY.Value;
                if (targetY <= currentY + 0.03f)
                {
                    pos.y = targetY;
                }
                else
                {
                    pos.y = currentY;
                }
            }
            else
            {
                pos.y = currentY;
            }

            return pos;
        }

        private void SnapToGroundHard()
        {
            Vector3 pos = transform.position;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                pos.y = hit.point.y;
                controlledPosition = pos;
                transform.position = pos;
            }
        }
        private void HandleCamera()
        {
            Vector3 eyeBase = headBone != null
                ? headBone.position + transform.up * (isCrouching ? 0.14f : 0.20f)
                : transform.position + transform.up * (isCrouching ? 1.52f : 1.92f);
            Vector3 desiredCamPos = eyeBase + transform.forward * 1.00f + transform.up * 0.04f;

            Vector3 camDir = desiredCamPos - eyeBase;
            float camDistance = camDir.magnitude;
            Vector3 camPos = desiredCamPos;
            if (camDistance > 0.001f)
            {
                Vector3 dirNorm = camDir / camDistance;
                var camHits = Physics.SphereCastAll(eyeBase, CameraCollisionRadius, dirNorm, camDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = camDistance;
                bool blocked = false;
                foreach (var h in camHits)
                {
                    if (h.collider == null || !IsBlockingCollider(h.collider))
                    {
                        continue;
                    }

                    blocked = true;
                    if (h.distance < nearest)
                    {
                        nearest = h.distance;
                    }
                }

                if (blocked)
                {
                    float safeDistance = Mathf.Clamp(nearest - 0.18f, 0.06f, camDistance);
                    camPos = eyeBase + dirNorm * safeDistance;
                }
            }

            myCamera.transform.position = camPos;
            myCamera.transform.rotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
        }


        private bool TryCapsuleMove(Vector3 start, Vector3 delta, out Vector3 result)
        {
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                result = start;
                return true;
            }

            Vector3 direction = delta / distance;
            Vector3 p1 = start + Vector3.up * CapsuleBottom;
            Vector3 p2 = start + Vector3.up * CapsuleTop;
            float nearest = distance;
            bool blocked = false;

            var hits = Physics.CapsuleCastAll(p1, p2, CollisionRadius, direction, distance + CollisionSkin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || !IsBlockingCollider(hit.collider))
                {
                    continue;
                }

                var rb = hit.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && hit.collider.transform.root != transform.root)
                {
                    rb.AddForceAtPosition(direction * DynamicPushForce, hit.point, ForceMode.Impulse);
                    continue;
                }

                if (hit.collider.bounds.max.y <= start.y + StepHeight + 0.02f)
                {
                    continue;
                }

                blocked = true;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            result = blocked
                ? start + direction * Mathf.Max(0f, nearest - CollisionSkin)
                : start + delta;
            return !blocked;
        }

        private bool IsBlockingCollider(Collider col)
        {
            if (col == null || col.isTrigger)
            {
                return false;
            }
            if (col.transform.root == transform.root)
            {
                return false;
            }

            Vector3 size = col.bounds.size;
            if (size.y < 0.20f && Mathf.Max(size.x, size.z) < 0.90f)
            {
                return false;
            }

            if (col.attachedRigidbody != null && size.y < 0.20f && Mathf.Max(size.x, size.z) < 0.70f)
            {
                return false;
            }

            return true;
        }

        private void UpdateCrouchAnimation()
        {
            if (anim == null)
            {
                return;
            }

            TrySetAnimatorBool("Crouching", isCrouching);
            TrySetAnimatorBool("Crouch", isCrouching);
            TrySetAnimatorBool("IsCrouching", isCrouching);
            TrySetAnimatorBool("IsCrouched", isCrouching);

            if (HasAnimatorParam("CrouchAmount", AnimatorControllerParameterType.Float))
            {
                anim.SetFloat("CrouchAmount", isCrouching ? 1f : 0f);
            }
        }

        private bool HasAnimatorParam(string name, AnimatorControllerParameterType type)
        {
            if (anim == null)
            {
                return false;
            }

            foreach (var p in anim.parameters)
            {
                if (p.type == type && p.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private void TrySetAnimatorBool(string name, bool value)
        {
            if (HasAnimatorParam(name, AnimatorControllerParameterType.Bool))
            {
                anim.SetBool(name, value);
            }
        }
        private void TryForwardInteraction()
        {
            if (aiLink == null)
            {
                return;
            }

            Vector3 probe = transform.position + Vector3.up * 1.0f + transform.forward * 1.0f;
            var cols = Physics.OverlapSphere(probe, AutoInteractDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (cols == null || cols.Length == 0)
            {
                return;
            }

            PlayerInteractListener nearestHandle = null;
            float bestHandle = float.MaxValue;
            OpenableInteractable nearestOpenable = null;
            float bestOpenable = float.MaxValue;
            PushableDoor nearestDoor = null;
            float bestDoor = float.MaxValue;

            foreach (var c in cols)
            {
                if (c == null)
                {
                    continue;
                }

                var handle = c.GetComponentInParent<PlayerInteractListener>();
                if (handle != null)
                {
                    float dh = Vector3.Distance(transform.position, handle.transform.position);
                    if (dh < bestHandle)
                    {
                        bestHandle = dh;
                        nearestHandle = handle;
                    }
                }

                var openable = c.GetComponentInParent<OpenableInteractable>();
                if (openable != null)
                {
                    float doo = Vector3.Distance(transform.position, openable.transform.position);
                    if (doo < bestOpenable)
                    {
                        bestOpenable = doo;
                        nearestOpenable = openable;
                    }
                }

                var doorCandidate = c.GetComponentInParent<PushableDoor>();
                if (doorCandidate != null && doorCandidate.CanBePushed)
                {
                    float dd = Vector3.Distance(transform.position, doorCandidate.transform.position);
                    if (dd < bestDoor)
                    {
                        bestDoor = dd;
                        nearestDoor = doorCandidate;
                    }
                }
            }

            if (nearestHandle != null && localGnome != null)
            {
                nearestHandle.Interact(localGnome);
                return;
            }

            if (nearestOpenable != null)
            {
                var type = nearestOpenable.InteractType;
                if (type == InteractableObject.Type.DOOR || type == InteractableObject.Type.FRIDGE_DOOR || type == InteractableObject.Type.OVEN_DOOR)
                {
                    nearestOpenable.UnClasp();
                    nearestOpenable.Open();
                    return;
                }
            }

            if (nearestDoor != null)
            {
                Vector3 forceDir = (nearestDoor.transform.position - transform.position).normalized;
                if (forceDir.sqrMagnitude < 0.0001f)
                {
                    forceDir = transform.forward;
                }

                var push = new IPlayerPushable.PushParams
                {
                    force = forceDir * 1.8f,
                    forcePosition = probe
                };
                nearestDoor.Push(push);
                return;
            }

            InteractableObject nearest = null;
            float best = float.MaxValue;
            foreach (var c in cols)
            {
                if (c == null)
                {
                    continue;
                }

                var i = c.GetComponentInParent<InteractableObject>();
                if (i == null || !i.CanHumanInteract)
                {
                    continue;
                }

                float d = Vector3.Distance(transform.position, i.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            if (nearest == null)
            {
                return;
            }

            var tInt = nearest.InteractType;
            bool canStuffPlayer = aiLink.kidnapper != null && aiLink.kidnapper.CurrentlyHeld != null &&
                                  (tInt == InteractableObject.Type.PLAYER_PUT_FRIDGE ||
                                   tInt == InteractableObject.Type.PLAYER_PUT_OVEN ||
                                   tInt == InteractableObject.Type.PLAYER_FLUSH_TOILET ||
                                   tInt == InteractableObject.Type.PLAYER_THROW_FIREPLACE ||
                                   tInt == InteractableObject.Type.PLAYER_THROW_OUT_FRONTDOOR ||
                                   tInt == InteractableObject.Type.PLAYER_KIDNAP_DESTINATION ||
                                   tInt == InteractableObject.Type.PLAYER_HIDING_SPOT);

            if (!canStuffPlayer)
            {
                return;
            }

            var held = aiLink.kidnapper.CurrentlyHeld;
            if (held == null)
            {
                return;
            }

            held.Teleport(nearest.HumanHoldPosition);
            aiLink.kidnapper.CurrentlyHeld = null;
            if (anim != null)
            {
                anim.SetBool("Carrying", false);
            }
        }

        private void TryGrabPlayer()
        {
            if (GameProgressionManager.AllPlayers == null)
            {
                return;
            }

            PlayerNetworking nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 handPoint = transform.position + Vector3.up * 1.2f + transform.forward * ExtendedHandForward;

            foreach (var player in GameProgressionManager.AllPlayers)
            {
                if (player == null || player == localGnome)
                {
                    continue;
                }

                float distanceFromBody = Vector3.Distance(transform.position, player.transform.position);
                if (distanceFromBody > GrabRange)
                {
                    continue;
                }

                float distanceFromHand = Vector3.Distance(handPoint, player.transform.position);
                if (distanceFromHand > ExtendedHandRadius)
                {
                    continue;
                }

                if (distanceFromHand < nearestDistance)
                {
                    nearestDistance = distanceFromHand;
                    nearest = player;
                }
            }

            if (nearest != null)
            {
                SendAction(ActionGrab, nearest.OwnerClientId);
            }
        }

        private void SendAction(byte actionId, ulong targetClientId)
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            if (NetworkManager.Singleton.IsServer)
            {
                GrandpaModPatches.ExecuteGrandpaAction(actionId, targetClientId);
                return;
            }

            using (var writer = new FastBufferWriter(sizeof(byte) + sizeof(ulong), Allocator.Temp))
            {
                writer.WriteValueSafe(actionId);
                writer.WriteValueSafe(targetClientId);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("GrandpaActionMsg", NetworkManager.ServerClientId, writer);
            }
        }
    }
}


















































