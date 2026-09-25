using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;  // 单例，方便其他脚本调用

    public AudioSource bgmSource;         // 背景音乐的 Audio Source
    public AudioSource endingSource;      // 结局音乐的 Audio Source

    public AudioClip bgmClip;             // 背景音乐文件
    public AudioClip endingClip;          // 结局音乐文件

    void Awake()
    {
        // 单例模式，确保只有一个 MusicManager
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);  // 切换场景时不销毁
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 设置背景音乐的 Audio Source
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.clip = bgmClip;
        bgmSource.loop = true;              // 背景音乐循环
        bgmSource.volume = 0.5f;
        bgmSource.Play();                   // 开始播放背景音乐

        // 设置结局音乐的 Audio Source
        if (endingSource == null)
        {
            endingSource = gameObject.AddComponent<AudioSource>();
        }
        endingSource.clip = endingClip;
        endingSource.loop = false;           // 结局音乐通常不循环
        endingSource.volume = 0.7f;
        endingSource.playOnAwake = false;
    }

    // 切换到结局音乐的方法
    public void SwitchToEndingMusic()
    {
        bgmSource.Stop();                    // 停止背景音乐
        endingSource.Play();                 // 播放结局音乐
    }

    // 如果想要淡出效果，可以用这个方法
    public void SwitchToEndingMusicWithFade(float fadeTime = 1f)
    {
        StartCoroutine(FadeOutBGM(fadeTime));
    }

    System.Collections.IEnumerator FadeOutBGM(float fadeTime)
    {
        float startVolume = bgmSource.volume;
        
        // 背景音乐淡出
        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= startVolume * Time.deltaTime / fadeTime;
            yield return null;
        }
        
        bgmSource.Stop();
        bgmSource.volume = startVolume;  // 恢复音量，下次用
        
        endingSource.Play();              // 播放结局音乐
    }
}