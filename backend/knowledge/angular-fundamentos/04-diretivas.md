---
slug: angular-diretivas
title: Diretivas Estruturais e de Atributo
order: 4
level: Beginner
tags: [directive, structural, attribute, ngif, ngfor, ngclass]
readMinutes: 8
---

## Para que serve

Diretivas são classes que modificam o comportamento ou a aparência de elementos do DOM. O Angular reconhece três tipos: componentes (diretivas com template), diretivas estruturais (mudam o layout do DOM adicionando ou removendo elementos) e diretivas de atributo (mudam aparência ou comportamento de um elemento existente). Diretivas estendem o vocabulário do HTML com semântica específica da aplicação.

## Diretivas estruturais

Diretivas estruturais alteram a estrutura do DOM. As mais comuns historicamente são `*ngIf`, `*ngFor` e `*ngSwitch`. O asterisco é açúcar sintático que o Angular expande para a forma com `<ng-template>`. Por exemplo, `*ngIf="condicao"` é equivalente a um bloco `<ng-template [ngIf]="condicao">` que só é instanciado quando a condição é verdadeira.

```ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-lista-pedidos',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="pedidos.length > 0; else vazio">
      <ul>
        <li *ngFor="let pedido of pedidos; let i = index; trackBy: porId">
          {{ i + 1 }}. {{ pedido.descricao }} — R$ {{ pedido.valor }}
        </li>
      </ul>
    </div>

    <ng-template #vazio>
      <p>Nenhum pedido encontrado.</p>
    </ng-template>
  `
})
export class ListaPedidosComponent {
  pedidos = [
    { id: 1, descricao: 'Mouse', valor: 89.90 },
    { id: 2, descricao: 'Teclado', valor: 250.00 }
  ];

  porId(_indice: number, pedido: { id: number }) {
    return pedido.id;
  }
}
```

A função `trackBy` em `*ngFor` melhora drasticamente a performance ao reutilizar nós DOM existentes quando os dados mudam. Sem ela, o Angular recria todos os elementos da lista a cada atualização, mesmo que apenas um item tenha sido alterado.

## A nova sintaxe de fluxo de controle

A partir do Angular 17, a sintaxe baseada em palavras-chave (`@if`, `@for`, `@switch`) substitui as diretivas estruturais clássicas. A nova forma é mais legível, mais rápida e não exige importação do `CommonModule`. Para novos projetos, é a forma recomendada.

```html
@if (carregando) {
  <p>Carregando dados...</p>
} @else if (erro) {
  <p class="alert alert-danger">{{ erro }}</p>
} @else {
  @for (item of itens; track item.id; let idx = $index) {
    <article>
      <h3>{{ idx + 1 }}. {{ item.titulo }}</h3>
    </article>
  } @empty {
    <p>Lista vazia.</p>
  }
}
```

As duas formas coexistem no mesmo projeto. Equipes podem migrar gradualmente; o Angular oferece uma ferramenta de schematic para converter `*ngIf` e `*ngFor` em `@if` e `@for` automaticamente.

## Diretivas de atributo

Diretivas de atributo mudam aparência ou comportamento de um elemento sem alterar a estrutura do DOM. As mais usadas são `ngClass`, `ngStyle` e `ngModel`. A `ngClass` permite aplicar classes condicionalmente a partir de um objeto, string ou array. A `ngStyle` faz o mesmo para propriedades de estilo inline.

```html
<div [ngClass]="{
  'ativo': item.selecionado,
  'erro': item.temErro,
  'destaque': item.prioridade > 5
}">
  {{ item.titulo }}
</div>

<p [ngStyle]="{
  'color': temaEscuro ? '#fff' : '#000',
  'background-color': cor,
  'font-size.px': tamanhoFonte
}">
  Texto estilizado
</p>
```

A escolha entre `ngClass` e `[class.x]` é estilística. Para uma ou duas classes, `[class.nome]="condicao"` é mais direto. Para muitas classes condicionais, `ngClass` com um objeto é mais legível.

## Criando uma diretiva de atributo customizada

Você pode criar suas próprias diretivas para encapsular comportamentos reutilizáveis. Uma diretiva de atributo é uma classe TypeScript decorada com `@Directive`, que recebe acesso ao elemento hospedeiro por injeção do `ElementRef`. Use `HostListener` para reagir a eventos e `HostBinding` para alterar propriedades do elemento hospedeiro.

```ts
import { Directive, ElementRef, HostListener, Input } from '@angular/core';

@Directive({
  selector: '[appDestaque]',
  standalone: true
})
export class DestaqueDirective {
  @Input() appDestaque = 'yellow';

  constructor(private el: ElementRef<HTMLElement>) {}

  @HostListener('mouseenter') aoEntrar() {
    this.el.nativeElement.style.backgroundColor = this.appDestaque;
  }

  @HostListener('mouseleave') aoSair() {
    this.el.nativeElement.style.backgroundColor = '';
  }
}
```

Para usar no template: `<p appDestaque="lightblue">Texto destacado ao passar o mouse</p>`. O Angular detecta o atributo `appDestaque` e instancia a diretiva, passando o valor `"lightblue"` como input.

## Diretivas estruturais customizadas

Diretivas estruturais são mais complexas porque manipulam um `ViewContainerRef` para criar ou destruir views a partir de um `TemplateRef`. Um exemplo clássico é uma diretiva `*appPermissao` que renderiza o conteúdo apenas se o usuário tem determinada permissão.

```ts
import { Directive, Input, TemplateRef, ViewContainerRef } from '@angular/core';

@Directive({
  selector: '[appPermissao]',
  standalone: true
})
export class PermissaoDirective {
  constructor(
    private template: TemplateRef<unknown>,
    private container: ViewContainerRef
  ) {}

  @Input() set appPermissao(role: string) {
    this.container.clear();
    if (this.usuarioTem(role)) {
      this.container.createEmbeddedView(this.template);
    }
  }

  private usuarioTem(_role: string): boolean {
    return true;
  }
}
```

## Armadilhas comuns

Misturar diretivas estruturais no mesmo elemento causa erro de compilação — não é possível usar `*ngIf` e `*ngFor` juntos. A solução é envolver com `<ng-container>` que não gera DOM. Esquecer o `trackBy` em listas grandes degrada performance silenciosamente. E ao criar diretivas customizadas, lembre que diretivas standalone precisam ser adicionadas ao array `imports` do componente que as usa.
