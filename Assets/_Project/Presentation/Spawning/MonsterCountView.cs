using LegionBreak.Application.Spawning;
using LegionBreak.Application.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LegionBreak.Presentation.Spawning
{
    /// <summary>
    /// MVP의 View. Presenter가 넘겨준 값을 그리기만 하고, 언제/무엇을 보여줄지는 판단하지
    /// 않는다(그 판단은 Application.UI.MonsterCountPresenter가 한다). Presenter는 Unity
    /// 생명주기가 없어 Update에서 매 프레임 Tick을 호출해 구동한다.
    ///
    /// Presenter를 DI로 주입받지 않고 View가 직접 생성하는 이유: Presenter 생성자가
    /// IMonsterCountView(=View 자신)를 요구하는데, View↔Presenter를 둘 다 컨테이너가
    /// 만들게 하면 서로가 서로를 필요로 하는 순환 의존이 되어 VContainer가 자기 자신을
    /// 참조하는 Lazy&lt;T&gt;에 걸려 예외를 던진다(런타임에서 실제로 확인됨). View는 순환에
    /// 관여하지 않는 IMonsterSpawner만 주입받고, 그걸로 Presenter를 직접 생성해 순환을 끊는다.
    /// </summary>
    public class MonsterCountView : MonoBehaviour, IMonsterCountView
    {
        [SerializeField] private Text _countText;

        private MonsterCountPresenter _presenter;

        [Inject]
        public void Construct(IMonsterSpawner spawner)
        {
            _presenter = new MonsterCountPresenter(spawner, this);
        }

        public void SetCount(int count)
        {
            if (_countText != null)
            {
                _countText.text = $"Monsters: {count}";
            }
        }

        private void Update()
        {
            _presenter?.Tick(Time.deltaTime);
        }
    }
}
