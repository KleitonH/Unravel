---
slug: angular-signals
title: Signals (Visão Geral)
order: 12
level: Intermediate
tags: [signals, reactivity, computed, effect, state]
readMinutes: 9
---

## Para que serve

Signals são o novo sistema de reatividade do Angular, introduzido como preview no Angular 16 e estabilizado no Angular 17. Um signal é uma função que encapsula um valor reativo e notifica consumidores automaticamente quando esse valor muda. O sistema de signals oferece detecção de mudanças granular, performance superior ao Zone.js e uma API ergonômica para gerenciar estado dentro e fora de componentes.

## Criando e lendo signals

A função `signal()` cria um signal com um valor inicial. Para ler o valor, basta invocar o signal como função: `meuSignal()`. Para escrever, usa-se `set()` para substituir o valor completo ou `update()` para derivá-lo do anterior. Mutar o valor diretamente não dispara reatividade, então sempre use os métodos da API.

```ts
import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-contador',
  standalone: true,
  template: `
    <p>Valor: {{ contador() }}</p>
    <button (click)="incrementar()">+1</button>
    <button (click)="zerar()">Zerar</button>
  `
})
export class ContadorComponent {
  contador = signal(0);

  incrementar() {
    this.contador.update(v => v + 1);
  }

  zerar() {
    this.contador.set(0);
  }
}
```

No template, observe que o signal é chamado como `contador()`, não apenas `contador`. Essa invocação é o que registra a leitura como dependência reativa, permitindo ao Angular saber quais views precisam atualizar quando o valor muda.

## Computed signals

A função `computed()` cria um signal derivado de outros signals. O valor é calculado preguiçosamente e em cache; só é recomputado quando alguma dependência muda e quando algum consumidor lê. Computed signals são imutáveis — não têm `set()` nem `update()`.

```ts
import { Component, signal, computed } from '@angular/core';

@Component({
  selector: 'app-carrinho',
  standalone: true,
  template: `
    <p>Itens: {{ qtde() }}</p>
    <p>Total: R$ {{ total() }}</p>
    <p>Frete grátis? {{ freteGratis() ? 'Sim' : 'Não' }}</p>
  `
})
export class CarrinhoComponent {
  itens = signal([
    { nome: 'Mouse', preco: 89.9 },
    { nome: 'Teclado', preco: 250 }
  ]);

  qtde = computed(() => this.itens().length);
  total = computed(() => this.itens().reduce((s, i) => s + i.preco, 0));
  freteGratis = computed(() => this.total() >= 200);
}
```

Computed signals encadeiam-se naturalmente. O `freteGratis` depende de `total`, que depende de `itens`. Quando `itens` muda, o Angular invalida `total` e `freteGratis`, mas só recomputa quando algum template os lê de fato.

## Effects: reagindo a mudanças

A função `effect()` executa um callback sempre que algum signal lido dentro dele muda. Effects são úteis para sincronizar estado com o mundo externo — escrever em localStorage, fazer logging, integrar com bibliotecas que não usam signals. Effects devem ser criados em um contexto de injeção (construtor, campo de classe).

```ts
import { Component, signal, effect } from '@angular/core';

@Component({
  selector: 'app-tema',
  standalone: true,
  template: `<button (click)="alternar()">Alternar tema</button>`
})
export class TemaComponent {
  modo = signal<'claro' | 'escuro'>('claro');

  constructor() {
    effect(() => {
      document.body.dataset['theme'] = this.modo();
      localStorage.setItem('tema', this.modo());
    });
  }

  alternar() {
    this.modo.update(m => m === 'claro' ? 'escuro' : 'claro');
  }
}
```

O Angular cancela automaticamente o effect quando o contexto de injeção é destruído — em um componente, isso acontece junto com o `ngOnDestroy`. Não é necessário desinscrever manualmente.

## Atualizando objetos e arrays

Como signals usam comparação por referência para detectar mudanças, mutar diretamente um objeto ou array dentro de um signal não dispara reatividade. Sempre crie uma nova referência ao atualizar.

```ts
import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-tarefas',
  standalone: true,
  template: `
    @for (t of tarefas(); track t.id) {
      <li>{{ t.titulo }}</li>
    }
  `
})
export class TarefasComponent {
  tarefas = signal<{ id: number; titulo: string }[]>([]);

  adicionar(titulo: string) {
    this.tarefas.update(lista => [...lista, { id: Date.now(), titulo }]);
  }

  remover(id: number) {
    this.tarefas.update(lista => lista.filter(t => t.id !== id));
  }
}
```

Spread operator e métodos imutáveis como `filter` e `map` são os padrões recomendados. Quem prefere mutações inline pode usar a função `mutate()` em versões anteriores, mas ela foi removida em favor da abordagem explícita.

## Signals como inputs de componentes

A partir do Angular 17.1, é possível declarar inputs como signals usando a função `input()`. Essa forma substitui o decorator `@Input()` clássico e integra-se naturalmente com `computed` e `effect`. Inputs como signals são read-only por design.

```ts
import { Component, input, computed } from '@angular/core';

@Component({
  selector: 'app-saudacao',
  standalone: true,
  template: `<p>{{ mensagem() }}</p>`
})
export class SaudacaoComponent {
  nome = input.required<string>();
  formal = input(false);

  mensagem = computed(() =>
    this.formal()
      ? `Prezado(a) ${this.nome()}`
      : `Olá, ${this.nome()}!`
  );
}
```

A versão `input.required<T>()` torna o input obrigatório em tempo de compilação. Se um consumidor esquecer de passar o valor, o build falha — uma melhoria significativa sobre `@Input()` com `!`.

## Outputs e queries como funções

O modelo de signal-based APIs estende-se a outputs e queries. A função `output()` substitui `@Output() new EventEmitter()`. As funções `viewChild()`, `viewChildren()`, `contentChild()` e `contentChildren()` substituem os decorators correspondentes, devolvendo signals.

```ts
import { Component, viewChild, ElementRef, AfterViewInit, output } from '@angular/core';

@Component({
  selector: 'app-foco',
  standalone: true,
  template: `<input #campo />`
})
export class FocoComponent implements AfterViewInit {
  campo = viewChild<ElementRef<HTMLInputElement>>('campo');
  pronto = output<void>();

  ngAfterViewInit() {
    this.campo()?.nativeElement.focus();
    this.pronto.emit();
  }
}
```

Essas APIs convergem todo o modelo do Angular em torno de signals, tornando o código mais consistente e habilitando otimizações futuras de change detection.

## Signals vs RxJS

Signals e RxJS não competem; são ferramentas complementares. Signals são síncronos, projetados para estado de UI e cálculos derivados. RxJS é ideal para fluxos assíncronos: eventos, requisições HTTP, web sockets, debounce, throttle, combinação de streams. A regra prática é usar signals para "como o estado atual deve ser apresentado" e RxJS para "como eventos no tempo se transformam em estado".

A interoperabilidade existe via funções `toSignal()` (converte observable em signal) e `toObservable()` (converte signal em observable), ambas em `@angular/core/rxjs-interop`. Isso permite consumir APIs RxJS dentro de componentes baseados em signals sem fricção.

## Armadilhas comuns

Esquecer de invocar o signal no template (`{{ contador }}` em vez de `{{ contador() }}`) renderiza a referência da função em vez do valor. Mutar diretamente arrays ou objetos dentro de signals não dispara atualização — sempre use `set` ou `update` com nova referência. Criar `effect()` fora de um contexto de injeção lança erro em runtime. Por fim, cuidado ao ler signals dentro de loops ou estruturas que rodam muitas vezes — cada leitura cria uma dependência reativa que pode causar recomputos excessivos se não for projetada com cuidado.
