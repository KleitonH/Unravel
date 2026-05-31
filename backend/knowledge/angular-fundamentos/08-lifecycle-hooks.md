---
slug: angular-lifecycle-hooks
title: Lifecycle Hooks
order: 8
level: Intermediate
tags: [lifecycle, hooks, ngoninit, ngondestroy, change-detection]
readMinutes: 8
---

## Para que serve

Lifecycle hooks são métodos especiais que o Angular chama em momentos específicos do ciclo de vida de um componente ou diretiva. Hooks permitem reagir a eventos como criação da instância, recebimento de novos inputs, renderização, mudanças de propriedades e destruição. Cada hook tem um propósito bem definido e é executado em uma ordem garantida.

## A ordem de execução

Quando o Angular cria um componente, ele invoca os hooks em uma sequência precisa: `constructor` primeiro, depois `ngOnChanges` (se houver inputs), `ngOnInit`, `ngDoCheck`, `ngAfterContentInit`, `ngAfterContentChecked`, `ngAfterViewInit` e `ngAfterViewChecked`. Durante a vida útil do componente, `ngOnChanges`, `ngDoCheck`, `ngAfterContentChecked` e `ngAfterViewChecked` são executados a cada ciclo de detecção de mudanças. No final, `ngOnDestroy` é chamado uma única vez.

```ts
import { Component, OnInit, OnDestroy, Input, OnChanges, SimpleChanges } from '@angular/core';

@Component({
  selector: 'app-monitor',
  standalone: true,
  template: `<p>{{ valor }}</p>`
})
export class MonitorComponent implements OnInit, OnDestroy, OnChanges {
  @Input() valor = 0;

  ngOnChanges(changes: SimpleChanges) {
    console.log('Mudou:', changes['valor']);
  }

  ngOnInit() {
    console.log('Componente iniciado');
  }

  ngOnDestroy() {
    console.log('Componente destruído');
  }
}
```

Implementar a interface correspondente (`OnInit`, `OnDestroy`) é opcional em runtime, mas recomendado para autocomplete e verificação de tipo no editor.

## ngOnInit: inicialização

O `ngOnInit` é o hook mais utilizado. Ele é chamado uma única vez, logo após a primeira invocação de `ngOnChanges`. Use `ngOnInit` para lógica de inicialização que depende de inputs do componente, como chamadas HTTP iniciais, assinatura de observables ou cálculo de estado derivado.

Por que não fazer essas inicializações no construtor? O construtor é executado quando a instância é criada, antes do Angular ter populado os inputs. Operações que dependem de `@Input()` falham silenciosamente quando colocadas no construtor. Além disso, manter o construtor enxuto facilita testes unitários.

```ts
@Component({ selector: 'app-perfil', standalone: true, template: `<p>{{ dados?.nome }}</p>` })
export class PerfilComponent implements OnInit {
  @Input() usuarioId!: number;
  dados: { nome: string } | null = null;

  constructor(private servico: UsuarioService) {}

  ngOnInit() {
    this.servico.buscar(this.usuarioId).subscribe(u => this.dados = u);
  }
}
```

## ngOnChanges: reagindo a inputs

O `ngOnChanges` é chamado sempre que um ou mais inputs do componente mudam de valor. O parâmetro `SimpleChanges` é um objeto que mapeia o nome de cada input alterado para um `SimpleChange` contendo `previousValue`, `currentValue` e `firstChange`. Esse hook é a forma correta de reagir a mudanças vindas do componente pai.

Atenção: `ngOnChanges` detecta apenas mudanças de referência, não mutações internas. Se o pai passa um array e adiciona um item com `push`, `ngOnChanges` não dispara. Para reagir a mutações profundas, é necessário criar uma nova referência (`this.lista = [...this.lista, item]`) ou usar `ngDoCheck`.

## ngOnDestroy: limpeza

O `ngOnDestroy` é chamado pouco antes do componente ser removido do DOM. É o local apropriado para cancelar assinaturas de observables, desconectar listeners de eventos globais, limpar timers e liberar recursos. Esquecer esse hook é a principal causa de vazamentos de memória em apps Angular.

```ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, interval, takeUntil } from 'rxjs';

@Component({ selector: 'app-relogio', standalone: true, template: `{{ hora }}` })
export class RelogioComponent implements OnInit, OnDestroy {
  hora = '';
  private destroy$ = new Subject<void>();

  ngOnInit() {
    interval(1000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.hora = new Date().toLocaleTimeString());
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

Uma alternativa moderna é o `DestroyRef`, que permite registrar callbacks de destruição sem implementar a interface. Combinado com `takeUntilDestroyed`, elimina o padrão do `Subject`.

## ngAfterViewInit: acesso ao DOM e a filhos

O `ngAfterViewInit` é chamado uma única vez após a view do componente e todas as views filhas serem inicializadas. Esse é o momento mais cedo em que você pode acessar elementos do template via `@ViewChild` ou componentes filhos pela API deles. Tentar acessar essas referências em `ngOnInit` retorna `undefined`.

```ts
import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';

@Component({
  selector: 'app-foco',
  standalone: true,
  template: `<input #campo type="text" />`
})
export class FocoComponent implements AfterViewInit {
  @ViewChild('campo') campo!: ElementRef<HTMLInputElement>;

  ngAfterViewInit() {
    this.campo.nativeElement.focus();
  }
}
```

## Hooks de conteúdo projetado

Quando um componente usa `<ng-content>` para projetar conteúdo do pai, surgem dois hooks adicionais. O `ngAfterContentInit` é chamado após o conteúdo projetado ser inicializado. O `ngAfterContentChecked` roda a cada ciclo de detecção de mudanças. Esses hooks são usados em conjunto com `@ContentChild` e `@ContentChildren`.

A distinção entre view (template próprio do componente) e content (template projetado) é crucial. View hooks lidam com o template definido em `template`/`templateUrl`. Content hooks lidam com o conteúdo passado por dentro das tags do componente.

## DestroyRef: a alternativa moderna

O `DestroyRef`, introduzido no Angular 16, oferece uma forma funcional de registrar lógica de destruição sem implementar `OnDestroy`. Ele é especialmente útil em funções utilitárias e composables que não têm acesso a hooks de classe.

```ts
import { Component, DestroyRef, inject } from '@angular/core';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({ selector: 'app-stream', standalone: true, template: `{{ valor }}` })
export class StreamComponent {
  valor = 0;

  constructor() {
    interval(1000)
      .pipe(takeUntilDestroyed())
      .subscribe(v => this.valor = v);
  }
}
```

A função `takeUntilDestroyed` precisa ser chamada em um contexto de injeção (campo ou construtor), porque ela usa `inject(DestroyRef)` internamente.

## Armadilhas comuns

Tentar usar `@ViewChild` em `ngOnInit` retorna `undefined` — só funciona a partir de `ngAfterViewInit`. Esquecer de cancelar subscriptions em `ngOnDestroy` causa vazamento de memória que só aparece em produção. Usar `ngDoCheck` ou `ngAfterViewChecked` com lógica pesada degrada performance porque esses hooks rodam em todo ciclo de detecção. Por fim, lembre que `ngOnChanges` não detecta mutações profundas; para isso, mude a referência ou use signals.
