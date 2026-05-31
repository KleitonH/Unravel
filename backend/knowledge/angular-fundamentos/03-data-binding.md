---
slug: angular-data-binding
title: Data Binding
order: 3
level: Beginner
tags: [binding, property, event, two-way, ngmodel]
readMinutes: 8
---

## Para que serve

Data binding é o mecanismo do Angular que sincroniza o estado da classe do componente com o template. O data binding elimina a necessidade de manipulação manual do DOM e mantém a interface sempre consistente com os dados. Angular oferece quatro formas de binding: interpolação, property binding, event binding e two-way binding.

## Property binding com colchetes

O property binding define o valor de uma propriedade do DOM ou de uma diretiva. A sintaxe usa colchetes ao redor do nome da propriedade. O lado direito é uma expressão que será avaliada e atribuída à propriedade. Property binding é unidirecional, do componente para o template.

```ts
@Component({
  selector: 'app-imagem',
  standalone: true,
  template: `
    <img [src]="urlImagem" [alt]="descricao" [width]="largura" />
    <button [disabled]="!podeClicar">Clique aqui</button>
    <input [value]="textoInicial" />
  `
})
export class ImagemComponent {
  urlImagem = '/assets/foto.jpg';
  descricao = 'Foto de perfil';
  largura = 200;
  podeClicar = false;
  textoInicial = 'Olá';
}
```

A diferença entre interpolação e property binding é sutil mas importante. Interpolação funciona apenas em atributos string. Property binding atualiza a propriedade real do elemento, preservando o tipo do valor. Por exemplo, `[disabled]="false"` desabilita o botão corretamente, enquanto `disabled="{{ false }}"` ainda mantém o botão desabilitado porque o atributo HTML `disabled` é interpretado por presença, não por valor.

## Event binding com parênteses

O event binding escuta eventos disparados pelo DOM ou por componentes filhos e executa uma expressão ou método em resposta. A sintaxe usa parênteses ao redor do nome do evento. A variável especial `$event` carrega o objeto do evento, que pode ser um `MouseEvent`, `KeyboardEvent` ou um valor emitido por um `EventEmitter`.

```ts
@Component({
  selector: 'app-busca',
  standalone: true,
  template: `
    <input
      (input)="atualizarTermo($event)"
      (keyup.enter)="pesquisar()"
      placeholder="Buscar..." />

    <button (click)="pesquisar()">Buscar</button>
    <button (click)="limpar(); $event.stopPropagation()">Limpar</button>
  `
})
export class BuscaComponent {
  termo = '';

  atualizarTermo(evento: Event) {
    this.termo = (evento.target as HTMLInputElement).value;
  }

  pesquisar() {
    console.log('Buscando:', this.termo);
  }

  limpar() {
    this.termo = '';
  }
}
```

É possível usar pseudo-eventos como `keyup.enter`, `keyup.escape` ou `keydown.control.s` para filtrar combinações de teclas específicas. Essa sintaxe declarativa evita o ruído de verificar `event.key === 'Enter'` manualmente.

## Two-way binding com banana-in-a-box

O two-way binding combina property binding e event binding em uma única sintaxe: `[(propriedade)]`. Essa notação é apelidada de "banana in a box" pela forma visual dos parênteses dentro dos colchetes. O exemplo clássico é o `ngModel` do `FormsModule`, que sincroniza o valor de um input com uma propriedade da classe em ambas as direções.

```ts
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cadastro',
  standalone: true,
  imports: [FormsModule],
  template: `
    <input [(ngModel)]="nome" placeholder="Seu nome" />
    <p>Olá, {{ nome || 'visitante' }}!</p>
  `
})
export class CadastroComponent {
  nome = '';
}
```

Internamente, `[(ngModel)]="nome"` é açúcar sintático para `[ngModel]="nome" (ngModelChange)="nome = $event"`. Qualquer componente pode implementar essa convenção, expondo um `@Input()` chamado `propriedade` e um `@Output()` chamado `propriedadeChange`. Quando ambos seguem essa nomenclatura, o consumidor pode usar a sintaxe abreviada.

## Attribute binding

Algumas situações exigem definir atributos HTML que não têm correspondência com propriedades do DOM, como `aria-label`, `colspan` em tabelas ou atributos de SVG. Nesses casos, usa-se o prefixo `attr.` no binding. Sem esse prefixo, o Angular tentaria atualizar uma propriedade inexistente e lançaria um erro.

```html
<button [attr.aria-label]="rotulo">×</button>

<td [attr.colspan]="numeroColunas">Cabeçalho</td>

<svg>
  <circle [attr.cx]="x" [attr.cy]="y" [attr.r]="raio" />
</svg>
```

A regra prática é: se a propriedade existe no objeto DOM (como `disabled`, `value`, `src`), use property binding com colchetes. Se for um atributo puro do HTML ou SVG, use `[attr.nome]`.

## Class binding e style binding

Class binding aplica ou remove classes CSS com base em expressões. Existem três formas: a binária `[class.nome]="condicao"` adiciona a classe quando a condição é verdadeira; a forma de string `[class]="'classe1 classe2'"` substitui todas as classes; e a forma de objeto `[ngClass]="{ ativo: x, erro: y }"` aceita várias classes condicionalmente.

```html
<div [class.destaque]="estaSelecionado">Item</div>

<div [ngClass]="{ ativo: usuario.online, premium: usuario.plano === 'pro' }">
  {{ usuario.nome }}
</div>

<div [class]="classesDinamicas">Classes vindas do componente</div>
```

Style binding funciona de forma análoga com `[style.propriedade]` para uma única propriedade ou `[ngStyle]` para um objeto. Sempre que possível, prefira class binding, porque CSS é mais performático e separa apresentação de lógica.

## Armadilhas comuns

Confundir property binding com atribuição de atributo HTML é um dos erros mais comuns. Lembre que `<div class="ativo">` é o atributo inicial, mas `<div [class]="estado">` substitui toda a classe a cada mudança. Outro erro é usar two-way binding sem importar o `FormsModule` em componentes standalone — sem essa importação, o `ngModel` simplesmente não funciona e a IDE pode não sinalizar claramente. Por fim, em event handlers, evite lógica pesada inline no template; delegue para métodos da classe para facilitar testes e manutenção.
