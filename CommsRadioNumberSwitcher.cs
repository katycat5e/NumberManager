using System.Collections.Generic;
using System.Linq;
using DV;
using DV.ThingTypes;
using HarmonyLib;
using UnityEngine;

namespace NumberManagerMod
{
    public class CommsRadioNumberSwitcher : MonoBehaviour, ICommsRadioMode
    {
        public static CommsRadioController Controller;

        public ButtonBehaviourType ButtonBehaviour { get; private set; }

        public CommsRadioDisplay display;
        public Transform signalOrigin;
        public Material selectionMaterial;
        public Material skinningMaterial;
        public GameObject trainHighlighter;

        // Sounds
        public AudioClip HoverCarSound;
        public AudioClip SelectedCarSound;
        public AudioClip ConfirmSound;
        public AudioClip CancelSound;

        private State CurrentState;
        private LayerMask TrainCarMask;
        private RaycastHit Hit;
        private TrainCar SelectedCar = null;
        private TrainCar PointedCar = null;
        private MeshRenderer HighlighterRender;

        private readonly int[] SelectedNumber = new int[4];
        private const int MAX_NUMBER = 9999;

        private const float SIGNAL_RANGE = 100f;
        private static readonly Vector3 HIGHLIGHT_BOUNDS_EXTENSION = new Vector3(0.25f, 0.8f, 0f);
        private static readonly Color LASER_COLOR = new Color(1f, 1f, 1f);
        public Color GetLaserBeamColor()
        {
            return LASER_COLOR;
        }
        public void OverrideSignalOrigin( Transform signalOrigin ) => this.signalOrigin = signalOrigin;

        #region Initialization

        public void Awake()
        {
            // steal components from other radio modes
            CommsRadioCarDeleter deleter = Controller.deleteControl;

            if( deleter != null )
            {
                signalOrigin = deleter.signalOrigin;
                display = deleter.display;
                selectionMaterial = new Material(deleter.selectionMaterial);
                skinningMaterial = new Material(deleter.deleteMaterial);
                trainHighlighter = deleter.trainHighlighter;

                // sounds
                HoverCarSound = deleter.hoverOverCar;
                SelectedCarSound = deleter.warningSound;
                ConfirmSound = deleter.confirmSound;
                CancelSound = deleter.cancelSound;
            }
            else
            {
                Debug.LogError("CommsRadioNumberSwitcher: couldn't get properties from siblings");
            }
        }

        public void Start()
        {
            if( !signalOrigin )
            {
                Debug.LogError("CommsRadioNumberSwitcher: signalOrigin on isn't set, using this.transform!", this);
                signalOrigin = transform;
            }

            if( display == null )
            {
                Debug.LogError("CommsRadioNumberSwitcher: display not set, can't function properly!", this);
            }

            if( (selectionMaterial == null) || (skinningMaterial == null) )
            {
                Debug.LogError("CommsRadioNumberSwitcher: Selection material(s) not set. Visuals won't be correct.", this);
            }

            if( trainHighlighter == null )
            {
                Debug.LogError("CommsRadioNumberSwitcher: trainHighlighter not set, can't function properly!!", this);
            }

            if( (HoverCarSound == null) || (SelectedCarSound == null) || (ConfirmSound == null) || (CancelSound == null) )
            {
                Debug.LogError("Not all audio clips set, some sounds won't be played!", this);
            }

            TrainCarMask = LayerMask.GetMask(new string[]
            {
                "Train_Big_Collider"
            });

            HighlighterRender = trainHighlighter.GetComponentInChildren<MeshRenderer>(true);
            trainHighlighter.SetActive(false);
            trainHighlighter.transform.SetParent(null);
        }

        public void Enable() { }

        public void Disable()
        {
            ResetState();
        }

        public void SetStartingDisplay()
        {
            string content = "Aim at the vehicle you wish to re-number.";
            display.SetDisplay("NUMBER", content, "");
        }

        #endregion

        #region Car Highlighting

        private void HighlightCar( TrainCar car, Material highlightMaterial )
        {
            if( car == null )
            {
                Debug.LogError("Highlight car is null. Ignoring request.");
                return;
            }

            HighlighterRender.material = highlightMaterial;

            trainHighlighter.transform.localScale = car.Bounds.size + HIGHLIGHT_BOUNDS_EXTENSION;
            Vector3 b = car.transform.up * (trainHighlighter.transform.localScale.y / 2f);
            Vector3 b2 = car.transform.forward * car.Bounds.center.z;
            Vector3 position = car.transform.position + b + b2;

            trainHighlighter.transform.SetPositionAndRotation(position, car.transform.rotation);
            trainHighlighter.SetActive(true);
            trainHighlighter.transform.SetParent(car.transform, true);
        }

        private void ClearHighlightedCar()
        {
            trainHighlighter.SetActive(false);
            trainHighlighter.transform.SetParent(null);
        }

        private void PointToCar( TrainCar car )
        {
            if( PointedCar != car )
            {
                if( car != null )
                {
                    PointedCar = car;
                    HighlightCar(PointedCar, selectionMaterial);
                    CommsRadioController.PlayAudioFromRadio(HoverCarSound, transform);
                }
                else
                {
                    PointedCar = null;
                    ClearHighlightedCar();
                }
            }
        }

        #endregion

        #region State Machine Actions

        private void SetState( State newState )
        {
            if( newState == CurrentState ) return;

            CurrentState = newState;
            switch( CurrentState )
            {
                case State.SelectCar:
                    SetStartingDisplay();
                    ButtonBehaviour = ButtonBehaviourType.Regular;
                    break;

                case State.SelectNumberThousand:
                    UpdateNumberFromCar(SelectedCar);
                    UpdateNumberDisplay();
                    ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.SelectNumberHundred:
                case State.SelectNumberTen:
                case State.SelectNumberOne:
                    UpdateNumberDisplay();
                    ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
            }
        }

        private void ResetState()
        {
            PointedCar = null;

            SelectedCar = null;
            ClearHighlightedCar();

            SetState(State.SelectCar);
        }

        public void OnUpdate()
        {
            TrainCar trainCar;

            switch( CurrentState )
            {
                case State.SelectCar:
                    if( !(SelectedCar == null) )
                    {
                        Debug.LogError("Invalid setup for current state, reseting flags!", this);
                        ResetState();
                        return;
                    }

                    // Check if not pointing at anything
                    if( !Physics.Raycast(signalOrigin.position, signalOrigin.forward, out Hit, SIGNAL_RANGE, TrainCarMask) )
                    {
                        PointToCar(null);
                    }
                    else
                    {
                        // Try to get the traincar we're pointing at
                        trainCar = TrainCar.Resolve(Hit.transform.root);
                        PointToCar(trainCar);
                    }

                    break;

                case State.SelectNumberThousand:
                case State.SelectNumberHundred:
                case State.SelectNumberTen:
                    if( !Physics.Raycast(signalOrigin.position, signalOrigin.forward, out Hit, SIGNAL_RANGE, TrainCarMask) )
                    {
                        PointToCar(null);
                        display.SetAction("cancel");
                    }
                    else
                    {
                        trainCar = TrainCar.Resolve(Hit.transform.root);
                        PointToCar(trainCar);
                        display.SetAction("next");
                    }

                    break;

                case State.SelectNumberOne:
                    if( !Physics.Raycast(signalOrigin.position, signalOrigin.forward, out Hit, SIGNAL_RANGE, TrainCarMask) )
                    {
                        PointToCar(null);
                        display.SetAction("cancel");
                    }
                    else
                    {
                        trainCar = TrainCar.Resolve(Hit.transform.root);
                        PointToCar(trainCar);
                        display.SetAction("confirm");
                    }

                    break;

                default:
                    ResetState();
                    break;
            }
        }

        public void OnUse()
        {
            switch( CurrentState )
            {
                case State.SelectCar:
                    if( PointedCar != null )
                    {
                        SelectedCar = PointedCar;
                        PointedCar = null;

                        HighlightCar(SelectedCar, skinningMaterial);
                        CommsRadioController.PlayAudioFromRadio(SelectedCarSound, transform);
                        SetState(State.SelectNumberThousand);
                    }
                    break;

                case State.SelectNumberThousand:
                case State.SelectNumberHundred:
                case State.SelectNumberTen:
                    if( (PointedCar != null) && (PointedCar == SelectedCar) )
                    {
                        // clicked on the selected car again, this means move to the next digit
                        CommsRadioController.PlayAudioFromRadio(ConfirmSound, transform);

                        State nextState = CurrentState + 1;
                        SetState(nextState);
                    }
                    else
                    {
                        // clicked off the selected car, this means cancel
                        CommsRadioController.PlayAudioFromRadio(CancelSound, transform);
                        ResetState();
                    }

                    break;

                case State.SelectNumberOne:
                    if( (PointedCar != null) && (PointedCar == SelectedCar) )
                    {
                        // clicked on the selected car again, this means confirm
                        ApplySelectedNumber();
                        CommsRadioController.PlayAudioFromRadio(ConfirmSound, transform);
                    }
                    else
                    {
                        // clicked off the selected car, this means cancel
                        CommsRadioController.PlayAudioFromRadio(CancelSound, transform);
                    }

                    ResetState();
                    break;
            }
        }

        public bool ButtonACustomAction()
        {
            if( CurrentState >= State.SelectNumberThousand )
            {
                int digIdx = (int)CurrentState - 1;

                int targetDig = SelectedNumber[digIdx] - 1;
                if( targetDig < 0 ) return false;

                SelectedNumber[digIdx] = targetDig;
                UpdateNumberDisplay();

                return true;
            }
            else
            {
                Debug.LogError(string.Format("Unexpected state {0}!", CurrentState), this);
                return false;
            }
        }

        public bool ButtonBCustomAction()
        {
            if( CurrentState >= State.SelectNumberThousand )
            {
                int digIdx = (int)CurrentState - 1;

                int targetDig = SelectedNumber[digIdx] + 1;
                if( targetDig > 9 ) return false;

                SelectedNumber[digIdx] = targetDig;
                UpdateNumberDisplay();

                return true;
            }
            else
            {
                Debug.LogError(string.Format("Unexpected state {0}!", CurrentState), this);
                return false;
            }
        }

        #endregion

        #region Number Shenanigans

        private void UpdateNumberFromCar( TrainCar car )
        {
            int savedNum = NumberManager.GetCurrentCarNumber(car);
            int[] toCopy = { 0, 0, 0, 1 };

            if( savedNum >= 0 )
            {
                toCopy = NumberManager.GetDigits(savedNum, 4);
            }

            for( int i = 0; i < 4; i++ )
            {
                SelectedNumber[i] = toCopy[i];
            }
        }

        private int BuildNumber()
        {
            int result = 0;

            for( int i = 0; i < 4; i++ )
            {
                result += SelectedNumber[i] * DigitOffset[i];
            }

            return result;
        }

        private void ApplySelectedNumber()
        {
            if( SelectedCar == null )
            {
                Debug.LogWarning("Tried to reskin to null selection");
            }

            int num = BuildNumber();
            NumberManager.ApplyNumbering(SelectedCar, num);

            if (CarTypes.IsSteamLocomotive(SelectedCar.carLivery) && SelectedCar.rearCoupler.IsCoupled())
            {
                TrainCar attachedCar = SelectedCar.rearCoupler.coupledTo?.train;
                if ((attachedCar != null) && CarTypes.IsTender(attachedCar.carLivery))
                {
                    // car attached behind loco is tender
                    NumberManager.ApplyNumbering(attachedCar, num);
                }
            }
        }

        private void UpdateNumberDisplay()
        {
            object[] digits = SelectedNumber.Cast<object>().ToArray();
            string fmt = StateDigitFormat[(int)CurrentState];

            display.SetContent(string.Format(fmt, digits));
        }

        #endregion

        private enum State
        {
            SelectCar = 0,

            SelectNumberThousand = 1,
            SelectNumberHundred = 2,
            SelectNumberTen = 3,
            SelectNumberOne = 4,
        }

        private static readonly int[] DigitOffset = new int[] { 1000, 100, 10, 1 };
        private static readonly string[] StateDigitFormat = new string[]
        {
            null,
            "\xBB{0}\xAB {1} {2} {3}",
            "{0} \xBB{1}\xAB {2} {3}",
            "{0} {1} \xBB{2}\xAB {3}",
            "{0} {1} {2} \xBB{3}\xAB"
        };
    }

    [HarmonyPatch(typeof(CommsRadioController), "Awake")]
    static class CommsRadio_Awake_Patch
    {
        public static CommsRadioNumberSwitcher numSwitcher = null;

        static void Postfix( CommsRadioController __instance, List<ICommsRadioMode> ___allModes )
        {
            CommsRadioNumberSwitcher.Controller = __instance;
            numSwitcher = __instance.gameObject.AddComponent<CommsRadioNumberSwitcher>();
            ___allModes.Add(numSwitcher);
        }
    }
}
