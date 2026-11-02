// Plays one HapticStyle with a UIKit feedback generator.
// The style numbers match HapticStyle.cs. Each call makes a short-lived generator; the code builds with or
// without ARC, whichever the Xcode target uses.
#import <UIKit/UIKit.h>

static void PlayImpact(UIImpactFeedbackStyle style)
{
    UIImpactFeedbackGenerator* generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [generator impactOccurred];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}

static void PlaySelection(void)
{
    UISelectionFeedbackGenerator* generator = [[UISelectionFeedbackGenerator alloc] init];
    [generator selectionChanged];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}

static void PlayNotification(UINotificationFeedbackType type)
{
    UINotificationFeedbackGenerator* generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator notificationOccurred:type];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}

extern "C" void Haptics_Play(int style)
{
    switch (style)
    {
        case 1: // Selection
            PlaySelection();
            break;
        case 2: // Light
            PlayImpact(UIImpactFeedbackStyleLight);
            break;
        case 3: // Medium
            PlayImpact(UIImpactFeedbackStyleMedium);
            break;
        case 4: // Rigid (iOS 13+), heavy before that
            if (@available(iOS 13.0, *))
            {
                PlayImpact(UIImpactFeedbackStyleRigid);
            }
            else
            {
                PlayImpact(UIImpactFeedbackStyleHeavy);
            }
            break;
        case 5: // Heavy
            PlayImpact(UIImpactFeedbackStyleHeavy);
            break;
        case 6: // Success
            PlayNotification(UINotificationFeedbackTypeSuccess);
            break;
        case 7: // Failure
            PlayNotification(UINotificationFeedbackTypeError);
            break;
        default:
            break;
    }
}
