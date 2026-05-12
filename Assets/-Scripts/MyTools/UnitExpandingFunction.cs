using UnityEngine;

public static class UnitExpandingFunction
{

    /// <summary>
    /// 检测动画标签
    /// </summary>
    /// <param name="animator"></param>
    /// <param name="tagName"></param>
    /// <param name="animationIndex"></param>
    /// <returns></returns>
    public static bool CheckAnimationTag(this Animator animator, string tagName, int animationIndex = 0)
    {
        if (animator == null)
        {
            return false;
        }

        return animator.GetCurrentAnimatorStateInfo(animationIndex).IsTag(tagName);
    }

    /// <summary>
    /// 检测动画片段名称
    /// </summary>
    /// <param name="animator"></param>
    /// <param name="animationName"></param>
    /// <param name="animationIndex"></param>
    /// <returns></returns>
    public static bool CheckAnimationName(this Animator animator, string animationName, int animationIndex = 0)
    {
        if (animator == null)
        {
            return false;
        }

        return animator.GetCurrentAnimatorStateInfo(animationIndex).IsName(animationName);
    }

    public static bool CheckCurrentTagAnimationTimeIsLess(this Animator animator, string tagName, float time)
    {
        if (animator.CheckAnimationTag(tagName))
        {
            //如果当前动画状态的标准化时间小于传递的时间返回true 否则返回false
            return (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < time) ? true : false;
        }

        //默认返回false
        return false;
    }

    public static bool CheckCurrentTagAnimationTimeIsExceed(this Animator animator, string tagName, float time)
    {
        if (animator.CheckAnimationTag(tagName))
        {
            //如果当前动画状态的标准化时间大于传递的时间返回true 否则返回false
            return (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > time) ? true : false;
        }

        //默认返回false
        return false;
    }

    /// <summary>
    /// 锁定目标方向
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="target"></param>
    /// <param name="self"></param>
    /// <param name="lerpTime"></param>
    /// <returns></returns>
    public static Quaternion LockOnTarget(this Transform transform, Transform target,Transform self,float lerpTime)
    {
        if (target == null) return self.rotation;

        Vector3 targetDirection = (target.position - self.position).normalized;
        targetDirection.y = 0f;

        Quaternion newRotation = Quaternion.LookRotation(targetDirection);

        return Quaternion.Lerp(self.rotation, newRotation, lerpTime * Time.deltaTime);
    }
}
