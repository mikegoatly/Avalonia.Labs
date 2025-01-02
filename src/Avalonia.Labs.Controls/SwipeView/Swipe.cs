using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Labs.Controls.Base.Pan;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media.Transformation;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace Avalonia.Labs.Controls;

public class Swipe : Grid
{
    /// <summary>
    /// The currently active swipe. This is used to auto-close an open swipe when another swipe is opened.
    /// </summary>
    private static Swipe? _activeSwipe;

    /// <summary>
    /// The command to execute when the user reveals the left panel. If this is null and <see cref="Left"/> is 
    /// not null, the panel will remain revealed without executing a command.
    /// </summary>
    public static readonly StyledProperty<ICommand?> LeftCommandProperty =
        AvaloniaProperty.Register<Swipe, ICommand?>(nameof(LeftCommand));

    /// <summary>
    /// The command to execute when the user reveals the left panel. If this is null and <see cref="Left"/> is 
    /// not null, the panel will remain revealed without executing a command.
    /// </summary>
    public ICommand? LeftCommand
    {
        get => GetValue(LeftCommandProperty);
        set => SetValue(LeftCommandProperty, value);
    }

    /// <summary>
    /// The command to execute when the user reveals the right panel. If this is null and <see cref="Right"/> is
    /// not null, the panel will remain revealed without executing a command.
    /// </summary>
    public static readonly StyledProperty<ICommand?> RightCommandProperty =
        AvaloniaProperty.Register<Swipe, ICommand?>(nameof(RightCommand));

    /// <summary>
    /// The command to execute when the user swipes to the right.
    /// </summary>
    public ICommand? RightCommand
    {
        get => GetValue(RightCommandProperty);
        set => SetValue(RightCommandProperty, value);
    }

    public static readonly StyledProperty<DataTemplate> RightTemplateProperty =
        AvaloniaProperty.Register<Swipe, DataTemplate>(nameof(Right));


    /// <summary>
    /// LeftCommandParameter StyledProperty definition
    /// </summary>
    public static readonly StyledProperty<object?> LeftCommandParameterProperty =
        AvaloniaProperty.Register<Swipe, object?>(nameof(LeftCommandParameter));

    /// <summary>
    /// Gets or sets the LeftCommandParameter property.
    /// </summary>
    public object? LeftCommandParameter
    {
        get => GetValue(LeftCommandParameterProperty);
        set => SetValue(LeftCommandParameterProperty, value);
    }

    /// <summary>
    /// RightCommandParameter StyledProperty definition
    /// </summary>
    public static readonly StyledProperty<object?> RightCommandParameterProperty =
        AvaloniaProperty.Register<Swipe, object?>(nameof(RightCommandParameter));

    /// <summary>
    /// Gets or sets the RightCommandParameter property.
    /// </summary>
    public object? RightCommandParameter
    {
        get => GetValue(RightCommandParameterProperty);
        set => SetValue(RightCommandParameterProperty, value);
    }

    /// <summary>
    /// DataTemplate for the right side
    /// </summary>
    public DataTemplate Right
    {
        get => GetValue(RightTemplateProperty);
        set => SetValue(RightTemplateProperty, value);
    }

    public static readonly StyledProperty<DataTemplate> LeftTemplateProperty =
        AvaloniaProperty.Register<Swipe, DataTemplate>(nameof(Left));

    /// <summary>
    /// DataTemplate for the left side
    /// </summary>
    public DataTemplate Left
    {
        get => GetValue(LeftTemplateProperty);
        set => SetValue(LeftTemplateProperty, value);
    }

    public static readonly StyledProperty<Control> ContentProperty =
        AvaloniaProperty.Register<Swipe, Control>(nameof(Content));

    /// <summary>
    /// The content of the Swipe component
    /// </summary>
    public Control Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly StyledProperty<SwipeState> SwipeStateProperty =
        AvaloniaProperty.Register<Swipe, SwipeState>(nameof(SwipeState));

    /// <summary>
    /// The current state of the Swipe component
    /// </summary>
    public SwipeState SwipeState
    {
        get => GetValue(SwipeStateProperty);
        set => SetValue(SwipeStateProperty, value);
    }

    private readonly ContentPresenter _rightContainer;
    private readonly ContentPresenter _leftContainer;
    private readonly ContentPresenter _bodyContainer;
    private readonly TransformOperationsTransition _transition;
    private readonly PanGestureRecognizer _panGestureRecognizer;

    private double _initialX;
    private double _currentX;

    public Swipe()
    {
        _rightContainer = new ContentPresenter
        {
            IsVisible = false,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right
        };

        _leftContainer = new ContentPresenter
        {
            IsVisible = false,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };

        _bodyContainer = new ContentPresenter
        {
            Transitions = new Transitions()
        };

        _transition = new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseOut()
        };

        _panGestureRecognizer = new PanGestureRecognizer
        {
            Direction = PanDirection.None,
            Threshold = 10,
        };

        _panGestureRecognizer.OnPan += PanUpdated;

        _bodyContainer.GestureRecognizers.Add(_panGestureRecognizer);

        Children.Add(_rightContainer);
        Children.Add(_leftContainer);
        Children.Add(_bodyContainer);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
        {
            _bodyContainer.Content = change.NewValue;
            return;
        }
        else if (change.Property == SwipeStateProperty)
        {
            ProcessSwipe(SwipeState);
        }
        else if (change.Property == LeftTemplateProperty)
        {
            if (change.NewValue is not null)
            {
                _panGestureRecognizer.Direction |= PanDirection.Right;
            }
            else
            {
                _panGestureRecognizer.Direction &= ~PanDirection.Right;
            }
        }
        else if (change.Property == RightTemplateProperty)
        {
            if (change.NewValue is not null)
            {
                _panGestureRecognizer.Direction |= PanDirection.Left;
            }
            else
            {
                _panGestureRecognizer.Direction &= ~PanDirection.Left;
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        UnregisterAsActiveSwipe();

        base.OnDetachedFromLogicalTree(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnregisterAsActiveSwipe();

        base.OnDetachedFromVisualTree(e);
    }

    private SwipeState CalculateState(double translationX)
    {
        var stepSize = translationX < 0
            ? _rightContainer.Bounds.Width
            : _leftContainer.Bounds.Width;

        if (stepSize > Math.Abs(translationX))
        {
            return SwipeState.Hidden;
        }

        return translationX switch
        {
            < 0 => SwipeState.RightVisible,
            > 0 => SwipeState.LeftVisible,
            _ => SwipeState.Hidden
        };
    }

    private void ProcessSwipe(SwipeState state)
    {
        switch (state)
        {
            case SwipeState.RightVisible:
                ShowPanel(_rightContainer, Right, RightCommand, RightCommandParameter);
                break;
            case SwipeState.LeftVisible:
                ShowPanel(_leftContainer, Left, LeftCommand, LeftCommandParameter);
                break;
            case SwipeState.Hidden:
            default:
                _rightContainer.IsVisible = false;
                _leftContainer.IsVisible = false;
                SetTranslate(0);
                break;
        }
    }

    private void ShowPanel(ContentPresenter sidePanel, DataTemplate template, ICommand? command, object? commandParameter)
    {
        sidePanel.IsVisible = true;
        MaterializeDataTemplate(sidePanel, template);

        var direction = sidePanel == _rightContainer ? -1F : 1F;
        if (command is null)
        {
            // Reveal the panel
            SetTranslate(sidePanel.Bounds.Width * direction);
        }
        else
        {
            AnimatePanelExecution(sidePanel, direction);

            // Execute the command
            command.Execute(commandParameter);
        }
    }

    private void AnimatePanelExecution(ContentPresenter sidePanel, float direction)
    {
        var bodyPresenter = ElementComposition.GetElementVisual((_bodyContainer as Visual)!);
        if (bodyPresenter is not null)
        {
            // Create an animation to reveal the side panel
            var revealAnimation = bodyPresenter.Compositor.CreateVector3KeyFrameAnimation();
            var revealed = new Vector3((float)_bodyContainer.Bounds.Width * direction, 0F, 0F);
            var easing = new CircularEaseOut();
            revealAnimation.InsertKeyFrame(0.4F, revealed, easing);
            revealAnimation.InsertKeyFrame(0.6F, revealed);
            revealAnimation.InsertKeyFrame(1.0F, Vector3.Zero, easing);
            revealAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
            revealAnimation.Duration = TimeSpan.FromMilliseconds(400);
            revealAnimation.Target = "Offset";

            // Reset the explicit translation to 0 - we want the animation to take over
            SetTranslate(0);
            bodyPresenter.StartAnimation("Offset", revealAnimation);

            // Not sure if there's a better way to do this; we can't animate IsVisible
            // as part of the composition animation, so we need to do it separately
            Task.Delay(400).ContinueWith(
                _ =>
                {
                    // Ensure the side panel is hidden once the animation is complete
                    sidePanel.IsVisible = false;
                },
                TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    private void SetTranslate(double x)
    {
        _currentX = x;
        var transformOperation = TransformOperations.CreateBuilder(1);
        transformOperation.AppendTranslate(x, 0);

        _bodyContainer.SetValue(RenderTransformProperty, transformOperation.Build());
    }

    private void MaterializeDataTemplate(ContentPresenter contentView, DataTemplate? dataTemplate)
    {
        if (contentView.Content is not null || dataTemplate is null)
        {
            return;
        }

        var view = dataTemplate.Build(DataContext);
        contentView.Content = view;
    }

    private void PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case PanGestureStatus.Started:
                RegisterAsActiveSwipe();

                _initialX = _currentX;
                _bodyContainer.Transitions!.Remove(_transition);
                MaterializeDataTemplate(_rightContainer, Right);
                MaterializeDataTemplate(_leftContainer, Left);

                break;
            case PanGestureStatus.Running:
                var x = _initialX + e.TotalX;

                SetTranslate(x);

                _rightContainer.IsVisible = x < 0;
                _leftContainer.IsVisible = x > 0;

                break;
            case PanGestureStatus.Completed:
                _bodyContainer.Transitions!.Add(_transition);
                var newState = CalculateState(_initialX + e.TotalX);
                if (SwipeState == newState)
                {
                    ProcessSwipe(newState);
                    return;
                }

                SwipeState = newState;
                break;
        }
    }

    private void RegisterAsActiveSwipe()
    {
        if (_activeSwipe is not null && _activeSwipe != this)
        {
            _activeSwipe.SwipeState = SwipeState.Hidden;
        }

        _activeSwipe = this;
    }

    private void UnregisterAsActiveSwipe()
    {
        // TODO check this is enough once we have a list long enough to virtualize!
        if (_activeSwipe == this)
        {
            _activeSwipe = null;
        }
    }
}
